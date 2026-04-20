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
        var removed = new List<(int, long, long)>();

        foreach (var (relationId, rel) in relations.Relations)
        {
            if (!rel.IsActive) continue;

            var (_, posA) = SkillPipeline.FindEntityPosition(rel.AttackerId, maps);
            var (_, posB) = SkillPipeline.FindEntityPosition(rel.TargetId, maps);
            int dist = Distance(posA, posB);
            rel.LastDistance = dist;

            // 彻底逃离
            if (dist > GameConstants.DisengageDistanceB)
            {
                rel.DisengageTimer2 += dt;
                if (rel.DisengageTimer2 >= GameConstants.DisengageTimeS2)
                {
                    _logger.LogInformation("[Disengage] DistanceB triggered: relation={RelationId} A={A} B={B} dist={Dist}", relationId, rel.AttackerId, rel.TargetId, dist);
                    removed.Add((relationId, rel.AttackerId, rel.TargetId));
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
                        continue;
                    }
                }
                else rel.DisengageTimer1 = 0;
            }
            else rel.DisengageTimer1 = 0;
        }

        foreach (var (relationId, _, _) in removed)
            relations.RemoveRelation(relationId);

        return removed;
    }

    private static int Distance((int x, int y)? a, (int x, int y)? b)
    {
        if (a == null || b == null) return int.MaxValue;
        return Math.Abs(a.Value.x - b.Value.x) + Math.Abs(a.Value.y - b.Value.y);
    }
}
