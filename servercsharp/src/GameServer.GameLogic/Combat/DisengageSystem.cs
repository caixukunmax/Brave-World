using GameServer.Services.Core;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 脱战判定系统
/// </summary>
public class DisengageSystem
{
    private readonly ILogger _logger;

    public DisengageSystem(ILogger logger)
    {
        _logger = logger;
    }

    public List<(int relationId, long attackerId, long targetId)> Tick(double dt, CombatRelationManager relations, Dictionary<string, MapState> maps)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long nowMs = Environment.TickCount64;
        var removed = new List<(int, long, long)>();
        var bidirectionalRemovedPairs = new HashSet<(long, long)>(); // 避免重复处理同一对

        foreach (var (relationId, rel) in relations.Relations)
        {
            if (!rel.IsActive) continue;

            // 双向去重：如果该实体对已经处理过，跳过
            var pair = rel.AttackerId < rel.TargetId
                ? (rel.AttackerId, rel.TargetId)
                : (rel.TargetId, rel.AttackerId);
            if (bidirectionalRemovedPairs.Contains(pair)) continue;

            var (_, posA) = SkillPipeline.FindEntityPosition(rel.AttackerId, maps);
            var (_, posB) = SkillPipeline.FindEntityPosition(rel.TargetId, maps);
            int dist = Distance(posA, posB);
            rel.LastDistance = dist;

            // TODO: 领地内免疫脱战 — 需要怪物配置表支持领地范围后接入
            // if (IsInTerritory(rel.AttackerId, rel.TargetId, maps)) continue;

            // 彻底逃离
            if (dist > GameConstants.DisengageDistanceB)
            {
                rel.DisengageTimer2 += dt;
                if (rel.DisengageTimer2 >= GameConstants.DisengageTimeS2)
                {
                    _logger.LogInformation("[Disengage] DistanceB triggered: relation={RelationId} A={A} B={B} dist={Dist}", relationId, rel.AttackerId, rel.TargetId, dist);
                    removed.Add((relationId, rel.AttackerId, rel.TargetId));
                    bidirectionalRemovedPairs.Add(pair);
                    continue;
                }
            }
            else rel.DisengageTimer2 = 0;

            // 安全脱离
            if (dist > GameConstants.DisengageDistanceA)
            {
                bool noDamage = (now - rel.LastDamageTime) >= GameConstants.DisengageNoDamageTimeT1;
                if (noDamage)
                {
                    rel.DisengageTimer1 += dt;
                    if (rel.DisengageTimer1 >= GameConstants.DisengageTimeS1)
                    {
                        _logger.LogInformation("[Disengage] DistanceA triggered: relation={RelationId} A={A} B={B} dist={Dist}", relationId, rel.AttackerId, rel.TargetId, dist);
                        removed.Add((relationId, rel.AttackerId, rel.TargetId));
                        bidirectionalRemovedPairs.Add(pair);
                        continue;
                    }
                }
                else rel.DisengageTimer1 = 0;
            }
            else rel.DisengageTimer1 = 0;
        }

        // 双向移除：每条脱战的关系，同时断开两个方向
        foreach (var (relationId, attackerId, targetId) in removed)
        {
            relations.RemoveBidirectionalRelation(attackerId, targetId);
        }

        return removed;
    }

    private static int Distance((int x, int y)? a, (int x, int y)? b)
    {
        if (a == null || b == null) return int.MaxValue;
        return Math.Abs(a.Value.x - b.Value.x) + Math.Abs(a.Value.y - b.Value.y);
    }
}
