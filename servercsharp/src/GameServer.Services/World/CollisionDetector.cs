using GameServer.Common.Events;
using GameServer.Services.Core;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.World;

/// <summary>
/// 碰撞检测 — 统一检测实体间碰撞并发布事件
/// 不依赖 CombatManager，通过 EventBus 解耦
/// 所有实体（玩家、怪物、未来新类型）共用同一碰撞逻辑
/// </summary>
public class CollisionDetector
{
    private readonly EventBus _eventBus;
    private readonly ILogger<CollisionDetector> _logger;
    private readonly WorldState _worldState;

    public CollisionDetector(EventBus eventBus, ILogger<CollisionDetector> logger, WorldState worldState)
    {
        _eventBus = eventBus;
        _logger = logger;
        _worldState = worldState;
    }

    /// <summary>
    /// 统一碰撞检测：实体到达 (x,y) 后，检查相邻格是否有其他实体
    /// 玩家检查怪物，怪物检查玩家。未来新实体类型只需在此方法扩展。
    /// </summary>
    public void CheckEntityCollision(long entityId, string mapName, int x, int y, MapState map)
    {
        bool isPlayer = map.Players.ContainsKey(entityId);
        _logger.LogTrace("[Collision] CheckEntityCollision: entity={EntityId} isPlayer={IsPlayer} pos=({X},{Y}) map={Map}",
            entityId, isPlayer, x, y, mapName);

        var selfPositions = _worldState.GetCombatPositions(entityId)
            .Where(cp => cp.mapName == mapName)
            .ToList();
        if (selfPositions.Count == 0)
            selfPositions = new List<(string, int, int)> { (mapName, x, y) };

        if (isPlayer)
        {
            // 检查怪物碰撞 — 使用 footprint 战斗位置
            foreach (var (instanceId, m) in map.Monsters)
            {
                if (instanceId == entityId) continue;

                var enemyPositions = _worldState.GetCombatPositions(instanceId)
                    .Where(cp => cp.mapName == mapName)
                    .ToList();
                if (enemyPositions.Count == 0)
                    enemyPositions = new List<(string, int, int)> { (mapName, m.X, m.Y) };

                bool collides = IsAdjacent(selfPositions, enemyPositions);

                if (collides)
                {
                    _logger.LogWarning("[Collision] player={PlayerId} collides monster={MonsterId} self={Self} enemy={Enemy}",
                        entityId, instanceId,
                        string.Join(";", selfPositions.Select(cp => $"({cp.x},{cp.y})")),
                        string.Join(";", enemyPositions.Select(cp => $"({cp.x},{cp.y})")));
                    _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: instanceId, mapName));
                }
            }

            // 检查NPC碰撞（NPC不参与双格，用权威坐标）
            foreach (var (npcId, npc) in map.Npcs)
            {
                bool collides = selfPositions.Any(cp =>
                    Math.Abs(cp.x - npc.X) + Math.Abs(cp.y - npc.Y) <= 1);
                if (collides)
                {
                    _logger.LogInformation("[Collision] player={PlayerId} adjacent to NPC={NpcId}({NpcName}) at ({NX},{NY})",
                        entityId, npcId, npc.Name, npc.X, npc.Y);
                    _eventBus.Emit("NpcCollisionDetected", (playerId: entityId, npcInstanceId: npcId, mapName));
                }
            }
        }
        else
        {
            // 怪物检查玩家 — 使用 footprint 战斗位置
            foreach (var (accountId, p) in map.Players)
            {
                if (accountId == entityId) continue;

                var enemyPositions = _worldState.GetCombatPositions(accountId)
                    .Where(cp => cp.mapName == mapName)
                    .ToList();
                if (enemyPositions.Count == 0)
                    enemyPositions = new List<(string, int, int)> { (mapName, p.GridX, p.GridY) };

                bool collides = IsAdjacent(selfPositions, enemyPositions);

                if (collides)
                {
                    _logger.LogWarning("[Collision] monster={MonsterId} collides player={PlayerId} self={Self} enemy={Enemy}",
                        entityId, accountId,
                        string.Join(";", selfPositions.Select(cp => $"({cp.x},{cp.y})")),
                        string.Join(";", enemyPositions.Select(cp => $"({cp.x},{cp.y})")));
                    _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: accountId, mapName));
                }
            }
        }
    }

    private static bool IsAdjacent(List<(string mapName, int x, int y)> a, List<(string mapName, int x, int y)> b)
    {
        foreach (var pa in a)
            foreach (var pb in b)
                if (Math.Abs(pa.x - pb.x) + Math.Abs(pa.y - pb.y) <= 1)
                    return true;
        return false;
    }
}
