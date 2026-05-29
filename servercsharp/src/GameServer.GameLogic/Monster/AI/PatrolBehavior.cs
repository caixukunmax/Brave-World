using GameServer.Common.Config;
using GameServer.Services.Core;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 巡逻行为：在出生点附近使用寻路随机游走
/// </summary>
public class PatrolBehavior : IBehaviorHandler
{
    private readonly MapDataProvider _mapData;
    private static readonly Random _rng = new();

    public PatrolBehavior(MapDataProvider mapData) => _mapData = mapData;

    public (int x, int y)? Run(MonsterRuntimeState m, string mapName, Dictionary<long, PlayerStateView> players, IWorldState? world)
    {
        long now = Environment.TickCount64;
        if (m.LastMoveTime > 0 && (now - m.LastMoveTime) < (m.AiConfig.MoveIntervalMs ?? 2000))
            return null;

        int range = m.AiConfig.PatrolRange ?? 3;

        // 构造动态障碍回调（排除自身位置）
        Func<int, int, bool> isBlocked = (x, y) =>
        {
            if (x == m.X && y == m.Y) return false; // 自身位置不算阻挡
            return world?.IsOccupied(mapName, x, y) ?? false;
        };

        // 在巡逻范围内随机选一个目标点，用 BFS 寻路过去
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
            // 随机打乱后依次尝试寻路
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            // 尝试最多 5 个候选目标，避免全量 BFS 开销
            int tryCount = Math.Min(5, candidates.Count);
            for (int i = 0; i < tryCount; i++)
            {
                var (tx, ty) = candidates[i];
                var next = Pathfind.BfsNextStep(m.X, m.Y, tx, ty, mapName, _mapData, isBlocked);
                if (next != null)
                {
                    m.State = MonsterState.Patrol;
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
                    m.State = MonsterState.Patrol;
                    m.LastMoveTime = now;
                    return (nx, ny);
                }
            }
        }
        return null;
    }
}
