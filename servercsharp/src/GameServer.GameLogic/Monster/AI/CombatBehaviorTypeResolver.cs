using GameServer.Tables;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 决定怪物战斗行为类型：
/// 先吃 AI 表显式配置，再用技能射程自动推断。
/// </summary>
public static class CombatBehaviorTypeResolver
{
    public static (CombatBehaviorType type, int combatRange) Resolve(AiRow? aiRow, MonsterRow? monsterRow, LubanTableLoader tables)
    {
        var explicitType = aiRow?.Param1 ?? CombatBehaviorType.Auto;
        int skillRange = ResolveCombatRange(monsterRow, tables);

        if (explicitType != CombatBehaviorType.Auto)
            return (explicitType, explicitType == CombatBehaviorType.Melee ? 1 : Math.Max(1, skillRange));

        if (monsterRow == null || monsterRow.Skills.Count == 0)
            return (CombatBehaviorType.Melee, 1);

        if (skillRange <= 1)
            return (CombatBehaviorType.Melee, 1);

        bool hasCastSkill = monsterRow.Skills
            .Select(tables.GetSkill)
            .Any(skill => skill != null && skill.CastRange > 1 && skill.CastTime > 0);

        return hasCastSkill
            ? (CombatBehaviorType.Caster, skillRange)
            : (CombatBehaviorType.Ranged, skillRange);
    }

    private static int ResolveCombatRange(MonsterRow? monsterRow, LubanTableLoader tables)
    {
        if (monsterRow == null || monsterRow.Skills.Count == 0)
            return 1;

        int maxRange = 1;
        foreach (var skillId in monsterRow.Skills)
        {
            var skill = tables.GetSkill(skillId);
            if (skill == null) continue;
            maxRange = Math.Max(maxRange, skill.CastRange);
        }

        return maxRange;
    }
}
