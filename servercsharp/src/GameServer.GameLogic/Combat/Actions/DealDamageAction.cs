using GameServer.Services.Core;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Combat.Actions;

/// <summary>
/// 造成伤害 Action — 移植自 combat/actions/deal_damage.lua
/// 伤害公式: floor(atk * coefficient * (1 - def * 0.01))，最低 1
/// </summary>
public class DealDamageAction : ICombatAction
{
    private readonly ILogger _logger;

    public DealDamageAction(ILogger<DealDamageAction> logger)
    {
        _logger = logger;
    }

    public string ActionType => "DealDamage";

    public ActionResult Execute(long casterId, List<long> targets, ActionContext context)
    {
        var parameters = context.ActionParams ?? new Dictionary<string, object>();
        var results = new ActionResult { Success = true };

        string damageType = GetStr(parameters, "damageType", "physical");
        double coefficient = GetDouble(parameters, "coefficient", 1.0);

        foreach (var targetId in targets)
        {
            var (damage, dtype) = CalcDamage(casterId, targetId, damageType, coefficient, context.Maps);

            if (context.CombatManager != null)
            {
                context.CombatManager.ApplyDamage(casterId, targetId, damage, dtype, context.Maps);
            }
            else
            {
                _logger.LogWarning("[DealDamage] combatManager not found in context");
            }

            results.Results.Add(new DamageResult
            {
                TargetId = targetId,
                Damage = damage,
                DamageType = dtype,
            });
        }

        return results;
    }

    private static (int damage, string damageType) CalcDamage(long casterId, long targetId,
        string damageType, double coefficient, Dictionary<string, MapState>? maps)
    {
        int baseDamage = 10;
        int targetDef = 0;

        if (damageType == "physical")
        {
            int patk = GetEntityAttr(casterId, "patk", maps) ?? 10;
            int pdef = GetEntityAttr(targetId, "pdef", maps) ?? 5;
            baseDamage = patk;
            targetDef = pdef;
        }
        else
        {
            int matk = GetEntityAttr(casterId, "matk", maps) ?? 10;
            int mdef = GetEntityAttr(targetId, "mdef", maps) ?? 5;
            baseDamage = matk;
            targetDef = mdef;
        }

        int damage = (int)Math.Floor(baseDamage * coefficient * (1 - targetDef * 0.01));
        if (damage < 1) damage = 1;

        return (damage, damageType);
    }

    internal static int? GetEntityAttr(long entityId, string attrName, Dictionary<string, MapState>? maps)
    {
        if (maps == null) return null;
        foreach (var map in maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
            {
                return attrName switch
                {
                    "role_name" or "name" => null,
                    "agility" => p.Agility,
                    "patk" => p.Patk,
                    "matk" => p.Matk,
                    "pdef" => p.Pdef,
                    "mdef" => p.Mdef,
                    "hp" => p.Hp,
                    _ => null,
                };
            }
            if (map.Monsters.TryGetValue(entityId, out var m))
            {
                return attrName switch
                {
                    "hp" => m.Hp,
                    "level" => m.Level,
                    "patk" => m.Patk,
                    "matk" => m.Matk,
                    "pdef" => m.Pdef,
                    "mdef" => m.Mdef,
                    "agility" => m.Agility,
                    _ => null,
                };
            }
        }
        return null;
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
