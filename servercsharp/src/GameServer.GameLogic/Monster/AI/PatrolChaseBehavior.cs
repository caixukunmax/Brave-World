using GameServer.Common.Config;
using GameServer.Services.Core;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 巡逻+追击行为：有玩家靠近时追击，远离出生点时返回
/// 所有移动均使用 BFS 寻路，绕开动态障碍
/// </summary>
public class PatrolChaseBehavior : IBehaviorHandler
{
    private readonly MapDataProvider _mapData;
    private static readonly Random _rng = new();

    public PatrolChaseBehavior(MapDataProvider mapData) => _mapData = mapData;

    public (int x, int y)? Run(MonsterRuntimeState m, string mapName, Dictionary<long, PlayerStateView> players, IWorldState? world)
    {
        long now = Environment.TickCount64;
        var cfg = m.AiConfig;
        int maxChase = cfg.MaxChaseDistance ?? 8;
        int spawnDist = Pathfind.Manhattan(m.X, m.Y, m.SpawnX, m.SpawnY);

        // 2. 追击
        var target = FindNearestPlayer(m, players);
        int? targetX = target?.GridX;
        int? targetY = target?.GridY;

        // 构造动态障碍回调（排除自身位置与追击目标格，否则 BFS 永远到不了玩家所在格）
        Func<int, int, bool> isBlocked = (x, y) =>
        {
            if (x == m.X && y == m.Y) return false;
            if (targetX == x && targetY == y) return false;
            return world?.IsOccupied(mapName, x, y) ?? false;
        };

        // 1. 远离出生点，强制返回
        if (spawnDist > maxChase)
        {
            var next = Pathfind.BfsNextStep(m.X, m.Y, m.SpawnX, m.SpawnY, mapName, _mapData, isBlocked);
            if (next != null) { m.State = "return"; m.TargetId = null; m.LastMoveTime = now; return next; }
            return null;
        }

        if (target != null)
        {
            m.TargetId = target.AccountId;
            int targetDist = Pathfind.Manhattan(m.X, m.Y, target.GridX, target.GridY);
            // 已经贴身，停止移动，避免反复尝试进入玩家格导致弹回
            if (targetDist <= 1)
            {
                m.State = "idle";
                return null;
            }
            if (m.LastMoveTime == 0 || (now - m.LastMoveTime) >= (cfg.ChaseIntervalMs ?? 500))
            {
                var next = Pathfind.BfsNextStep(m.X, m.Y, target.GridX, target.GridY, mapName, _mapData, isBlocked);
                if (next != null) { m.State = "chase"; m.LastMoveTime = now; return next; }
            }
            m.State = "idle";
            return null;
        }

        m.TargetId = null;

        // 3. 返回/idle 切换
        if (m.State == "chase" || m.State == "return")
        {
            if (spawnDist <= (cfg.PatrolRange ?? 2)) { m.State = "idle"; m.TargetId = null; }
            else
            {
                var next = Pathfind.BfsNextStep(m.X, m.Y, m.SpawnX, m.SpawnY, mapName, _mapData, isBlocked);
                if (next != null) { m.State = "return"; m.LastMoveTime = now; return next; }
            }
        }

        // 4. 巡逻 — 使用寻路
        if (m.LastMoveTime == 0 || (now - m.LastMoveTime) >= (cfg.MoveIntervalMs ?? 2000))
        {
            int range = cfg.PatrolRange ?? 3;

            // 在巡逻范围内随机选目标点
            var candidates = new List<(int x, int y)>();
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    int tx = m.SpawnX + dx, ty = m.SpawnY + dy;
                    if (tx == m.X && ty == m.Y) continue;
                    if (_mapData.IsWalkable(mapName, tx, ty) && !isBlocked(tx, ty))
                        candidates.Add((tx, ty));
                }
            }

            if (candidates.Count > 0)
            {
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }

                int tryCount = Math.Min(5, candidates.Count);
                for (int i = 0; i < tryCount; i++)
                {
                    var (tx, ty) = candidates[i];
                    var next = Pathfind.BfsNextStep(m.X, m.Y, tx, ty, mapName, _mapData, isBlocked);
                    if (next != null)
                    {
                        m.State = "patrol";
                        m.LastMoveTime = now;
                        return next;
                    }
                }
            }

            // 寻路失败，回退到相邻格随机走
            int[][] dirs = [[0, -1], [0, 1], [-1, 0], [1, 0]];
            for (int i = dirs.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
            }

            foreach (var d in dirs)
            {
                int nx = m.X + d[0], ny = m.Y + d[1];
                if (Math.Abs(nx - m.SpawnX) <= range && Math.Abs(ny - m.SpawnY) <= range)
                {
                    if (_mapData.IsWalkable(mapName, nx, ny) && !isBlocked(nx, ny))
                    {
                        m.State = "patrol";
                        m.LastMoveTime = now;
                        return (nx, ny);
                    }
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
