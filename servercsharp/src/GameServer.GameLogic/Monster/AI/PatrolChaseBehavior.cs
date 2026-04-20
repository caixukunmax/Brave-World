using GameServer.Common.Config;
using GameServer.Services.Core;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 巡逻+追击行为：有玩家靠近时追击，远离出生点时返回
/// </summary>
public class PatrolChaseBehavior : IBehaviorHandler
{
    private readonly MapDataProvider _mapData;
    private static readonly Random _rng = new();

    public PatrolChaseBehavior(MapDataProvider mapData) => _mapData = mapData;

    public (int x, int y)? Run(MonsterRuntimeState m, string mapName, Dictionary<long, PlayerStateView> players)
    {
        long now = Environment.TickCount64;
        var cfg = m.AiConfig;
        int maxChase = cfg.MaxChaseDistance ?? 8;
        int spawnDist = Pathfind.Manhattan(m.X, m.Y, m.SpawnX, m.SpawnY);

        // 1. 远离出生点，强制返回
        if (spawnDist > maxChase)
        {
            var next = Pathfind.BfsNextStep(m.X, m.Y, m.SpawnX, m.SpawnY, mapName, _mapData);
            if (next != null) { m.State = "return"; m.TargetId = null; m.LastMoveTime = now; return next; }
            return null;
        }

        // 2. 追击
        var target = FindNearestPlayer(m, players);
        if (target != null)
        {
            int targetDist = Pathfind.Manhattan(m.X, m.Y, target.GridX, target.GridY);
            if (m.LastMoveTime == 0 || (now - m.LastMoveTime) >= (cfg.ChaseIntervalMs ?? 500))
            {
                if (targetDist > 0)
                {
                    var next = Pathfind.BfsNextStep(m.X, m.Y, target.GridX, target.GridY, mapName, _mapData);
                    if (next != null) { m.State = "chase"; m.TargetId = target.AccountId; m.LastMoveTime = now; return next; }
                }
            }
            m.State = "idle";
            return null;
        }

        // 3. 返回/idle 切换
        if (m.State == "chase" || m.State == "return")
        {
            if (spawnDist <= (cfg.PatrolRange ?? 2)) { m.State = "idle"; m.TargetId = null; }
            else
            {
                var next = Pathfind.BfsNextStep(m.X, m.Y, m.SpawnX, m.SpawnY, mapName, _mapData);
                if (next != null) { m.State = "return"; m.LastMoveTime = now; return next; }
            }
        }

        // 4. 巡逻
        if (m.LastMoveTime == 0 || (now - m.LastMoveTime) >= (cfg.MoveIntervalMs ?? 2000))
        {
            int[][] dirs = [[0, -1], [0, 1], [-1, 0], [1, 0]];
            for (int i = dirs.Length - 1; i > 0; i--) { int j = _rng.Next(i + 1); (dirs[i], dirs[j]) = (dirs[j], dirs[i]); }

            int range = cfg.PatrolRange ?? 3;
            foreach (var d in dirs)
            {
                int nx = m.X + d[0], ny = m.Y + d[1];
                if (Math.Abs(nx - m.SpawnX) <= range && Math.Abs(ny - m.SpawnY) <= range)
                {
                    if (_mapData.IsWalkable(mapName, nx, ny))
                    { m.State = "patrol"; m.LastMoveTime = now; return (nx, ny); }
                }
            }
        }
        return null;
    }

    private static PlayerStateView? FindNearestPlayer(MonsterRuntimeState m, Dictionary<long, PlayerStateView> players)
    {
        PlayerStateView? nearest = null;
        int minDist = m.AiConfig.AggroRange ?? 0;
        if (minDist <= 0) return null;
        foreach (var p in players.Values)
        {
            int dist = Pathfind.Manhattan(m.X, m.Y, p.GridX, p.GridY);
            if (dist <= minDist && (nearest == null || dist < minDist)) { nearest = p; minDist = dist; }
        }
        return nearest;
    }
}
