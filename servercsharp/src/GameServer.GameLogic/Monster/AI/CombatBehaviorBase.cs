using GameServer.Tables;
using GameServer.Common.Config;
using GameServer.Services.Core;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 战斗态行为基类：处理目标解析、追击节流、按接战距离追到可攻击范围�?/// </summary>
public abstract class CombatBehaviorBase : ICombatBehaviorHandler
{
    protected readonly MapDataProvider MapData;

    protected CombatBehaviorBase(MapDataProvider mapData) => MapData = mapData;

    public abstract CombatBehaviorType Type { get; }

    protected abstract int ResolveEngageRange(MonsterRuntimeState monster);
    protected abstract string HoldState { get; }
    protected abstract string ChaseState { get; }

    public (int x, int y)? Run(MonsterRuntimeState m, string mapName, Dictionary<long, PlayerStateView> players, IWorldState? world)
    {
        var target = ResolveTarget(m, players);
        if (target == null)
        {
            m.State = "combat";
            return null;
        }

        m.TargetId = target.AccountId;

        int targetDist = Pathfind.Manhattan(m.X, m.Y, target.GridX, target.GridY);
        int engageRange = Math.Max(1, ResolveEngageRange(m));
        if (targetDist <= engageRange)
        {
            m.State = HoldState;
            return null;
        }

        long now = Environment.TickCount64;
        long chaseIntervalMs = m.AiConfig.ChaseIntervalMs ?? GameConstants.MonsterAiTickMs;
        if (m.LastMoveTime != 0 && (now - m.LastMoveTime) < chaseIntervalMs)
        {
            m.State = "combat";
            return null;
        }

        Func<int, int, bool> isBlocked = (x, y) =>
        {
            if (x == m.X && y == m.Y) return false;
            if (x == target.GridX && y == target.GridY) return false;
            return world?.IsOccupied(mapName, x, y) ?? false;
        };

        var next = Pathfind.BfsNextStep(m.X, m.Y, target.GridX, target.GridY, mapName, MapData, isBlocked);
        if (next == null)
        {
            m.State = HoldState;
            return null;
        }

        m.State = ChaseState;
        m.LastMoveTime = now;
        return next;
    }

    protected static PlayerStateView? ResolveTarget(MonsterRuntimeState m, Dictionary<long, PlayerStateView> players)
    {
        if (m.TargetId.HasValue && players.TryGetValue(m.TargetId.Value, out var locked))
            return locked;

        PlayerStateView? nearest = null;
        int minDist = int.MaxValue;
        foreach (var player in players.Values)
        {
            int dist = Pathfind.Manhattan(m.X, m.Y, player.GridX, player.GridY);
            if (dist < minDist)
            {
                nearest = player;
                minDist = dist;
            }
        }

        return nearest;
    }
}
