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

        if (isPlayer)
        {
            // 检查怪物碰撞 — 使用双格战斗位置
            foreach (var (instanceId, m) in map.Monsters)
            {
                if (instanceId == entityId) continue;

                var combatPositions = _worldState.GetCombatPositions(instanceId)
                    .Where(cp => cp.mapName == mapName)
                    .ToList();
                if (combatPositions.Count == 0)
                    combatPositions = new List<(string, int, int)> { (mapName, m.X, m.Y) };

                bool collides = combatPositions.Any(cp =>
                    Math.Abs(cp.x - x) + Math.Abs(cp.y - y) <= 1);

                if (collides)
                {
                    _logger.LogWarning("[Collision] player={PlayerId} at ({PX},{PY}) collides monster={MonsterId} combatPositions={Positions}",
                        entityId, x, y, instanceId,
                        string.Join(";", combatPositions.Select(cp => $"({cp.x},{cp.y})")));
                    _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: instanceId, mapName));
                }
            }

            // 检查NPC碰撞（NPC不参与双格，用权威坐标）
            foreach (var (npcId, npc) in map.Npcs)
            {
                int dist = Math.Abs(npc.X - x) + Math.Abs(npc.Y - y);
                if (dist <= 1)
                {
                    _logger.LogInformation("[Collision] player={PlayerId} at ({PX},{PY}) adjacent to NPC={NpcId}({NpcName}) at ({NX},{NY})",
                        entityId, x, y, npcId, npc.Name, npc.X, npc.Y);
                    _eventBus.Emit("NpcCollisionDetected", (playerId: entityId, npcInstanceId: npcId, mapName));
                }
            }
        }
        else
        {
            // 怪物检查玩家 — 使用双格战斗位置
            foreach (var (accountId, p) in map.Players)
            {
                if (accountId == entityId) continue;

                var combatPositions = _worldState.GetCombatPositions(accountId)
                    .Where(cp => cp.mapName == mapName)
                    .ToList();
                if (combatPositions.Count == 0)
                    combatPositions = new List<(string, int, int)> { (mapName, p.GridX, p.GridY) };

                bool collides = combatPositions.Any(cp =>
                    Math.Abs(cp.x - x) + Math.Abs(cp.y - y) <= 1);

                if (collides)
                {
                    _logger.LogWarning("[Collision] monster={MonsterId} at ({MX},{MY}) collides player={PlayerId} combatPositions={Positions}",
                        entityId, x, y, accountId,
                        string.Join(";", combatPositions.Select(cp => $"({cp.x},{cp.y})")));
                    _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: accountId, mapName));
                }
            }
        }
    }
}
