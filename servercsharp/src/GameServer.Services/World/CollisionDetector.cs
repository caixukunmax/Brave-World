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
    public void CheckEntityCollision(long entityId, string mapName, int x, int y, MapInstance map)
    {
        // 先判断 entityId 是玩家还是怪物
        bool isPlayer = map.Players.ContainsKey(entityId);
        _logger.LogDebug("[Collision] CheckEntityCollision: entity={EntityId} isPlayer={IsPlayer} pos=({X},{Y}) map={Map}",
            entityId, isPlayer, x, y, mapName);

        if (isPlayer)
        {
            // 玩家到达新位置，检查相邻怪物
            foreach (var (instanceId, m) in map.Monsters)
            {
                if (instanceId == entityId) continue;
                int dist = Math.Abs(m.X - x) + Math.Abs(m.Y - y);
                _logger.LogDebug("[Collision]   vs monster={MonsterId} mpos=({MX},{MY}) dist={Dist} hit={Hit}",
                    instanceId, m.X, m.Y, dist, dist <= 1);
                if (dist <= 1)
                {
                    _logger.LogWarning("[Collision] TRIGGERED: player={PlayerId} at ({PX},{PY}) vs monster={MonsterId} at ({MX},{MY}) dist={Dist}",
                        entityId, x, y, instanceId, m.X, m.Y, dist);
                    _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: instanceId, mapName));
                }
            }
        }
        else
        {
            // 怪物到达新位置，检查相邻玩家
            foreach (var (accountId, p) in map.Players)
            {
                if (accountId == entityId) continue;
                int dist = Math.Abs(p.GridX - x) + Math.Abs(p.GridY - y);
                _logger.LogDebug("[Collision]   vs player={PlayerId} ppos=({PX},{PY}) dist={Dist} hit={Hit}",
                    accountId, p.GridX, p.GridY, dist, dist <= 1);
                if (dist <= 1)
                {
                    _logger.LogWarning("[Collision] TRIGGERED: monster={MonsterId} at ({MX},{MY}) vs player={PlayerId} at ({PX},{PY}) dist={Dist}",
                        entityId, x, y, accountId, p.GridX, p.GridY, dist);
                    _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: accountId, mapName));
                }
            }
        }
    }
}
