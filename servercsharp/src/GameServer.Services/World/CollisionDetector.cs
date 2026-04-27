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

    public CollisionDetector(EventBus eventBus, ILogger<CollisionDetector> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
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
            // 检查怪物碰撞
            foreach (var (instanceId, m) in map.Monsters)
            {
                if (instanceId == entityId) continue;
                int dist = Math.Abs(m.X - x) + Math.Abs(m.Y - y);
                if (dist <= 1)
                {
                    _logger.LogWarning("[Collision] player={PlayerId} at ({PX},{PY}) collides monster={MonsterId} at ({MX},{MY})",
                        entityId, x, y, instanceId, m.X, m.Y);
                    _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: instanceId, mapName));
                }
            }

            // 检查NPC碰撞（不触发战斗，触发NPC交互）
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
            foreach (var (accountId, p) in map.Players)
            {
                if (accountId == entityId) continue;
                int dist = Math.Abs(p.GridX - x) + Math.Abs(p.GridY - y);
                if (dist <= 1)
                {
                    _logger.LogWarning("[Collision] monster={MonsterId} at ({MX},{MY}) collides player={PlayerId} at ({PX},{PY})",
                        entityId, x, y, accountId, p.GridX, p.GridY);
                    _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: accountId, mapName));
                }
            }
        }
    }
}
