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
            var (damage, dtype) = CalcDamage(casterId, targetId, damageType, coefficient, context.Maps, context.CombatManager);

            var combatId = context.CombatManager?.GetCombatId(casterId) ?? 0;
            var cmLogger = context.CombatManager?.Logger;
            if (cmLogger != null)
                CombatTrace.DamageCalc(cmLogger, combatId, casterId, SkillPipeline.GetEntityName(casterId, context.Maps!),
                    targetId, SkillPipeline.GetEntityName(targetId, context.Maps!),
                    damageType == "physical" ? (GetEntityAttr(casterId, "patk", context.Maps) ?? 10) : (GetEntityAttr(casterId, "matk", context.Maps) ?? 10),
                    damageType == "physical" ? (GetEntityAttr(targetId, "pdef", context.Maps) ?? 5) : (GetEntityAttr(targetId, "mdef", context.Maps) ?? 5),
                    coefficient, damage, dtype);

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

    internal static (int damage, string damageType) CalcDamage(long casterId, long targetId,
        string damageType, double coefficient, Dictionary<string, MapState>? maps, CombatManager? combatManager = null)
    {
        int baseDamage = 10;
        int targetDef = 0;

        // 获取施法者的 buff 属性修正
        int casterBuffAtk = GetBuffAttrModifier(casterId, damageType == "physical" ? "patk" : "matk", maps, combatManager);
        int targetBuffDef = GetBuffAttrModifier(targetId, damageType == "physical" ? "pdef" : "mdef", maps, combatManager);

        if (damageType == "physical")
        {
            int patk = (GetEntityAttr(casterId, "patk", maps) ?? 10) + casterBuffAtk;
            int pdef = (GetEntityAttr(targetId, "pdef", maps) ?? 5) + targetBuffDef;
            baseDamage = patk;
            targetDef = pdef;
        }
        else
        {
            int matk = (GetEntityAttr(casterId, "matk", maps) ?? 10) + casterBuffAtk;
            int mdef = (GetEntityAttr(targetId, "mdef", maps) ?? 5) + targetBuffDef;
            baseDamage = matk;
            targetDef = mdef;
        }

        // 地形防御修正
        float defModifier = 1.0f;
        if (combatManager != null && maps != null)
        {
            var (tMapName, tPos) = SkillPipeline.FindEntityPosition(targetId, maps);
            if (tMapName != null && tPos != null)
                defModifier = combatManager.GetTerrainDefModifier(targetId, tMapName, tPos.Value.x, tPos.Value.y, damageType);
        }
        int adjustedDef = (int)(targetDef * defModifier);

        int damage = (int)Math.Floor(baseDamage * coefficient * (1 - adjustedDef * 0.01));
        if (damage < 1) damage = 1;

        return (damage, damageType);
    }

    /// <summary>获取实体的 buff 属性修正值</summary>
    private static int GetBuffAttrModifier(long entityId, string attrName, Dictionary<string, MapState>? maps, CombatManager? combatManager = null)
    {
        if (maps == null) return 0;
        foreach (var map in maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
                return p.Buffs.GetAttrModifier(attrName);
            if (map.Monsters.TryGetValue(entityId, out var m))
            {
                // 怪物的 buff 在 CombatContext 里，从 CombatManager 获取
                if (combatManager != null)
                {
                    var ctx = combatManager.RelationsMgr.Contexts.GetValueOrDefault(entityId);
                    if (ctx != null)
                        return ctx.Buffs.GetAttrModifier(attrName);
                }
                return 0;
            }
        }
        return 0;
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
