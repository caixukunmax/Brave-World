using GameServer.Common.Buffs;
using GameServer.Services.Core;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Combat.Actions;

/// <summary>
/// 净化 Action — 移除目标身上所有 Debuff
/// </summary>
public class PurifyAction : ICombatAction
{
    private readonly ILogger _logger;

    public PurifyAction(ILogger<PurifyAction> logger)
    {
        _logger = logger;
    }

    public string ActionType => "Purify";

    public ActionResult Execute(long casterId, List<long> targets, ActionContext context)
    {
        var result = new ActionResult { Success = true };

        foreach (var targetId in targets)
        {
            // 查找目标的 BuffContainer
            BuffContainer? buffs = null;
            if (context.Maps != null)
            {
                foreach (var map in context.Maps.Values)
                {
                    if (map.Players.TryGetValue(targetId, out var p))
                    {
                        buffs = p.Buffs;
                        break;
                    }
                }
            }

            // 也从 CombatContext 获取（战斗中）
            if (buffs == null && context.CombatManager != null)
            {
                var ctx = context.CombatManager.GetContext(targetId);
                buffs = ctx?.Buffs;
            }

            if (buffs == null) continue;

            int removed = buffs.RemoveDebuffs();

            var combatId = context.CombatManager?.GetCombatId(casterId) ?? 0;
            var cmLogger = context.CombatManager?.Logger;
            if (cmLogger != null)
                CombatTrace.BuffPurify(cmLogger, combatId, casterId, SkillPipeline.GetEntityName(casterId, context.Maps!),
                    targetId, SkillPipeline.GetEntityName(targetId, context.Maps!), removed);

            _logger.LogInformation("[Purify] caster={Caster} target={Target} removed={Count}", casterId, targetId, removed);
            result.BuffAppliedTargets.Add(targetId);
        }

        return result;
    }
}