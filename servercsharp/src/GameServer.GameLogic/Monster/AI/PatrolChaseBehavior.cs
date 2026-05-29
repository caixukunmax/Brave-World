using GameServer.Common.Config;
using GameServer.Services.Core;
using System.Linq;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 巡逻+追击+回归行为：
/// - 领地内无限追击
/// - 领地外追击超时后回归
/// - 脱战后回归巡逻点
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
        int territoryRadius = cfg.TerritoryRadius ?? 0;

        // ---- 1. 强制返回（超过 MaxChaseDistance，且不在领地内）----
        int spawnDist = Pathfind.Manhattan(m.X, m.Y, m.SpawnX, m.SpawnY);
        int maxChase = cfg.MaxChaseDistance ?? 10;
        bool inTerritory = territoryRadius > 0 && spawnDist <= territoryRadius;

        if (!inTerritory && spawnDist > maxChase)
        {
            var next = Pathfind.BfsNextStep(m.X, m.Y, m.SpawnX, m.SpawnY, mapName, _mapData, IsBlocked(m, world));
            if (next != null) { m.State = MonsterState.ForcedReturn; m.TargetId = null; m.InCombat = false; m.LastMoveTime = now; return next; }
            return null;
        }

        // ---- 3. 追击 ----
        // 回归状态下必须回到领地内，期间不主动追击
        if (m.State is not (MonsterState.Return or MonsterState.ForcedReturn))
        {
            var target = FindNearestPlayer(m, players);
            int? targetX = target?.GridX;
            int? targetY = target?.GridY;

            if (target != null)
            {
                m.TargetId = target.AccountId;
                int targetDist = Pathfind.Manhattan(m.X, m.Y, target.GridX, target.GridY);
                // 已经贴身，停止移动
                if (targetDist <= 1)
                {
                    m.State = MonsterState.Idle;
                    return null;
                }
                if (m.LastMoveTime == 0 || (now - m.LastMoveTime) >= (cfg.ChaseIntervalMs ?? 500))
                {
                    var next = Pathfind.BfsNextStep(m.X, m.Y, target.GridX, target.GridY, mapName, _mapData, IsBlocked(m, world, targetX, targetY));
                    if (next != null) { m.State = MonsterState.Chase; m.LastMoveTime = now; return next; }
                }
                m.State = MonsterState.Idle;
                return null;
            }
        }

        m.TargetId = null;

        // ---- 4. 返回/idle 切换（脱战后返回巡逻点或 Spawn）----
        if (m.State == MonsterState.Chase || m.State == MonsterState.Return || m.State == MonsterState.ForcedReturn)
        {
            // ForcedReturn 始终直接回 Spawn，不受 ReturnPatrolPoint 干扰
            if (m.State == MonsterState.ForcedReturn)
            {
                if (territoryRadius <= 0 || inTerritory)
                {
                    m.State = MonsterState.Patrol;
                    m.ReturnPatrolPoint = null;
                    m.ReturnPatrolIndex = -1;
                    m.ReturnCooldownEndMs = now + (long)((cfg.ChaseTimeout ?? 10.0) * 1000);
                }
                else
                {
                    var next = Pathfind.BfsNextStep(m.X, m.Y, m.SpawnX, m.SpawnY, mapName, _mapData, IsBlocked(m, world));
                    if (next != null) { m.State = MonsterState.ForcedReturn; m.LastMoveTime = now; return next; }
                }
            }
            // Return / Chase 状态处理巡逻点回归
            else if (m.ReturnPatrolPoint.HasValue)
            {
                var (px, py) = m.ReturnPatrolPoint.Value;
                int patrolDist = Pathfind.Manhattan(m.X, m.Y, px, py);
                if (patrolDist <= 1)
                {
                    // 到达巡逻点，必须回到领地内才能解除回归
                    if (territoryRadius <= 0 || inTerritory)
                    {
                        m.State = MonsterState.Patrol;
                        m.ReturnPatrolPoint = null;
                        m.ReturnPatrolIndex = -1;
                        m.ReturnCooldownEndMs = now + (long)((cfg.ChaseTimeout ?? 10.0) * 1000);
                    }
                    else
                    {
                        // 到达巡逻点但仍在领地外，清除巡逻点继续向 Spawn 回归
                        m.ReturnPatrolPoint = null;
                        m.ReturnPatrolIndex = -1;
                    }
                }
                else
                {
                    var next = Pathfind.BfsNextStep(m.X, m.Y, px, py, mapName, _mapData, IsBlocked(m, world));
                    if (next != null) { m.State = MonsterState.Return; m.TargetId = null; m.InCombat = false; m.LastMoveTime = now; return next; }
                }
            }
            else if (territoryRadius <= 0 || inTerritory)
            {
                // 无领地限制，或已在领地内，解除回归
                m.State = MonsterState.Patrol;
                m.TargetId = null;
                m.ReturnCooldownEndMs = now + (long)((cfg.ChaseTimeout ?? 10.0) * 1000);
            }
            else
            {
                var next = Pathfind.BfsNextStep(m.X, m.Y, m.SpawnX, m.SpawnY, mapName, _mapData, IsBlocked(m, world));
                if (next != null) { m.State = MonsterState.Return; m.LastMoveTime = now; return next; }
            }
        }

        // ---- 5. 巡逻 ----
        if (m.LastMoveTime == 0 || (now - m.LastMoveTime) >= (cfg.MoveIntervalMs ?? 2000))
        {
            return DoPatrol(m, mapName, world, now);
        }

        return null;
    }

    private (int x, int)? DoPatrol(MonsterRuntimeState m, string mapName, IWorldState? world, long now)
    {
        var cfg = m.AiConfig;

        // 优先使用配置表中的巡逻点
        if (cfg.PatrolPoints != null && cfg.PatrolPoints.Count > 0)
        {
            // 随机选一个巡逻点
            var pt = cfg.PatrolPoints[_rng.Next(cfg.PatrolPoints.Count)];
            var next = Pathfind.BfsNextStep(m.X, m.Y, pt.x, pt.y, mapName, _mapData, IsBlocked(m, world));
            if (next != null)
            {
                m.State = MonsterState.Patrol;
                m.LastMoveTime = now;
                return next;
            }
        }

        // 回退：在 PatrolRange 范围内随机选点
        int range = cfg.PatrolRange ?? 3;
        var candidates = new List<(int x, int y)>();
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                int tx = m.SpawnX + dx, ty = m.SpawnY + dy;
                if (tx == m.X && ty == m.Y) continue;
                if (_mapData.IsWalkable(mapName, tx, ty))
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
                var next = Pathfind.BfsNextStep(m.X, m.Y, tx, ty, mapName, _mapData, IsBlocked(m, world));
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
                if (_mapData.IsWalkable(mapName, nx, ny))
                {
                    m.State = MonsterState.Patrol;
                    m.LastMoveTime = now;
                    return (nx, ny);
                }
            }
        }

        return null;
    }

    private static Func<int, int, bool> IsBlocked(MonsterRuntimeState m, IWorldState? world, int? targetX = null, int? targetY = null)
    {
        return (x, y) =>
        {
            if (x == m.X && y == m.Y) return false;
            if (targetX == x && targetY == y) return false;
            return world?.IsOccupied(m.MapName, x, y) ?? false;
        };
    }

    private static PlayerStateView? FindNearestPlayer(MonsterRuntimeState m, Dictionary<long, PlayerStateView> players)
    {
        // 回归冷却期内不重新 Aggro
        if (Environment.TickCount64 < m.ReturnCooldownEndMs) return null;

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
