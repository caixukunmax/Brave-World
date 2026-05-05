using GameServer.Services.Core;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Combat.Actions;

/// <summary>
/// 治疗 Action — 恢复目标生命值
/// 公式: floor(matk * coefficient)，最低 1
/// </summary>
public class HealAction : ICombatAction
{
    private readonly ILogger _logger;

    public HealAction(ILogger<HealAction> logger)
    {
        _logger = logger;
    }

    public string ActionType => "Heal";

    public ActionResult Execute(long casterId, List<long> targets, ActionContext context)
    {
        var parameters = context.ActionParams ?? new Dictionary<string, object>();
        var results = new ActionResult { Success = true };

        string healType = GetStr(parameters, "damageType", "magical");
        double coefficient = GetDouble(parameters, "coefficient", 1.0);

        foreach (var targetId in targets)
        {
            var (healAmount, htype) = CalcHeal(casterId, targetId, healType, coefficient, context.Maps);

            var combatId = context.CombatManager?.GetCombatId(casterId) ?? 0;
            var cmLogger = context.CombatManager?.Logger;
            if (cmLogger != null)
                CombatTrace.HealApply(cmLogger, combatId, casterId, SkillPipeline.GetEntityName(casterId, context.Maps!),
                    targetId, SkillPipeline.GetEntityName(targetId, context.Maps!), healAmount, htype);

            if (context.CombatManager != null)
            {
                context.CombatManager.ApplyHeal(casterId, targetId, healAmount, htype, context.Maps);
            }
            else
            {
                _logger.LogWarning("[Heal] combatManager not found in context");
            }

            results.Results.Add(new DamageResult
            {
                TargetId = targetId,
                Damage = healAmount,
                DamageType = htype,
            });
        }

        return results;
    }

    private static (int healAmount, string healType) CalcHeal(long casterId, long targetId,
        string healType, double coefficient, Dictionary<string, MapState>? maps)
    {
        int baseHeal = 10;

        if (healType == "physical")
        {
            int patk = DealDamageAction.GetEntityAttr(casterId, "patk", maps) ?? 10;
            baseHeal = patk;
        }
        else
        {
            int matk = DealDamageAction.GetEntityAttr(casterId, "matk", maps) ?? 10;
            baseHeal = matk;
        }

        int healAmount = (int)Math.Floor(baseHeal * coefficient);
        if (healAmount < 1) healAmount = 1;

        return (healAmount, healType);
    }

    private static string GetStr(Dictionary<string, object> dict, string key, string def)
    {
        if (dict.TryGetValue(key, out var v) && v is string s) return s;
        return def;
    }

    private static double GetDouble(Dictionary<string, object> dict, string key, double def)
    {
        if (dict.TryGetValue(key, out var v))
        {
            if (v is double d) return d;
            if (v is int i) return i;
            if (v is float f) return f;
        }
        return def;
    }
}
