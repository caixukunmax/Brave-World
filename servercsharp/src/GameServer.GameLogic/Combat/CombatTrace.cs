using GameServer.Common.Buffs;
using GameServer.Services.Core;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 战斗流程追踪日志 — 用 [CombatTrace] 标签输出结构化日志
/// 方便事后 grep 过滤出完整战斗流程，验证是否有 bug
/// 
/// 日志格式：[CombatTrace] event=xxx | key=value | ...
/// 可用 grep "[CombatTrace]" 快速过滤
/// </summary>
public static class CombatTrace
{

    // ---- 战斗开始/结束 ----

    public static void CombatStart(ILogger logger, long combatId, long entityA, long entityB, string nameA, string nameB)
    {
        logger.LogInformation(
            "[CombatTrace] event=combat_start | combat={Combat} | a={EidA}({NameA}) | b={EidB}({NameB})",
            combatId, entityA, nameA, entityB, nameB);
    }

    public static void CombatEnd(ILogger logger, long combatId, long entityId, string name, string reason)
    {
        logger.LogInformation(
            "[CombatTrace] event=combat_end | combat={Combat} | entity={Eid}({Name}) | reason={Reason}",
            combatId, entityId, name, reason);
    }

    // ---- 先手攻击 ----

    public static void FirstStrike(ILogger logger, long combatId, long attackerId, string attackerName, long targetId, string targetName, int skillId)
    {
        logger.LogInformation(
            "[CombatTrace] event=first_strike | combat={Combat} | attacker={Aid}({AName}) | target={Tid}({TName}) | skill={Skill}",
            combatId, attackerId, attackerName, targetId, targetName, skillId);
    }

    // ---- 技能释放 6 阶段 ----

    public static void SkillPreCheck(ILogger logger, long combatId, long casterId, string casterName, int skillId, bool ok, string? err)
    {
        logger.LogDebug(
            "[CombatTrace] event=skill_precheck | combat={Combat} | caster={Cid}({CName}) | skill={Skill} | ok={Ok} | err={Err}",
            combatId, casterId, casterName, skillId, ok, err ?? "");
    }

    public static void SkillSelectTargets(ILogger logger, long combatId, long casterId, string casterName, int skillId, string targetType, List<long> targetIds)
    {
        logger.LogDebug(
            "[CombatTrace] event=skill_select_targets | combat={Combat} | caster={Cid}({CName}) | skill={Skill} | target_type={TType} | targets=[{Targets}]",
            combatId, casterId, casterName, skillId, targetType, string.Join(",", targetIds));
    }

    public static void SkillStartCast(ILogger logger, long combatId, long casterId, string casterName, int skillId, double castTime, int mpCost)
    {
        logger.LogDebug(
            "[CombatTrace] event=skill_start_cast | combat={Combat} | caster={Cid}({CName}) | skill={Skill} | cast_time={CastTime:F1}s | mp_cost={MpCost}",
            combatId, casterId, casterName, skillId, castTime, mpCost);
    }

    public static void SkillFinalValidation(ILogger logger, long combatId, long casterId, string casterName, int skillId, bool ok, List<long> finalTargets)
    {
        logger.LogDebug(
            "[CombatTrace] event=skill_final_validation | combat={Combat} | caster={Cid}({CName}) | skill={Skill} | ok={Ok} | final_targets=[{Targets}]",
            combatId, casterId, casterName, skillId, ok, string.Join(",", finalTargets));
    }

    public static void SkillExecuteAction(ILogger logger, long combatId, long casterId, string casterName, int skillId, string actionType, List<long> targetIds)
    {
        logger.LogDebug(
            "[CombatTrace] event=skill_execute_action | combat={Combat} | caster={Cid}({CName}) | skill={Skill} | action={Action} | targets=[{Targets}]",
            combatId, casterId, casterName, skillId, actionType, string.Join(",", targetIds));
    }

    public static void SkillEndCast(ILogger logger, long combatId, long casterId, string casterName, int skillId, bool isMiss, double cooldown)
    {
        logger.LogDebug(
            "[CombatTrace] event=skill_end_cast | combat={Combat} | caster={Cid}({CName}) | skill={Skill} | miss={Miss} | cd={Cd:F1}s",
            combatId, casterId, casterName, skillId, isMiss, cooldown);
    }

    public static void SkillCastResult(ILogger logger, long combatId, long casterId, string casterName, int skillId, string result)
    {
        logger.LogDebug(
            "[CombatTrace] event=skill_cast_result | combat={Combat} | caster={Cid}({CName}) | skill={Skill} | result={Result}",
            combatId, casterId, casterName, skillId, result);
    }

    // ---- 伤害 ----

    public static void DamageApply(ILogger logger, long combatId, long attackerId, string attackerName,
        long targetId, string targetName, int damage, string damageType, int absorbed, int effective)
    {
        logger.LogDebug(
            "[CombatTrace] event=damage_apply | combat={Combat} | attacker={Aid}({AName}) | target={Tid}({TName}) | dmg={Dmg} | type={Type} | absorbed={Absorbed} | effective={Effective}",
            combatId, attackerId, attackerName, targetId, targetName, damage, damageType, absorbed, effective);
    }

    public static void DamageCalc(ILogger logger, long combatId, long attackerId, string attackerName,
        long targetId, string targetName, int baseAtk, int targetDef, double coefficient, int finalDamage, string damageType)
    {
        logger.LogDebug(
            "[CombatTrace] event=damage_calc | combat={Combat} | attacker={Aid}({AName}) | target={Tid}({TName}) | base_atk={BaseAtk} | target_def={Def} | coef={Coef:F1} | final={Final} | type={Type}",
            combatId, attackerId, attackerName, targetId, targetName, baseAtk, targetDef, coefficient, finalDamage, damageType);
    }

    // ---- 治疗 ----

    public static void HealApply(ILogger logger, long combatId, long casterId, string casterName,
        long targetId, string targetName, int healAmount, string healType)
    {
        logger.LogDebug(
            "[CombatTrace] event=heal_apply | combat={Combat} | caster={Cid}({CName}) | target={Tid}({TName}) | heal={Heal} | type={Type}",
            combatId, casterId, casterName, targetId, targetName, healAmount, healType);
    }

    // ---- Buff ----

    public static void BuffApply(ILogger logger, long combatId, long casterId, string casterName,
        long targetId, string targetName, int buffId, string buffName, int stacks, double duration, int shield)
    {
        logger.LogDebug(
            "[CombatTrace] event=buff_apply | combat={Combat} | caster={Cid}({CName}) | target={Tid}({TName}) | buff={Bid}({BName}) | stacks={Stacks} | dur={Dur:F1}s | shield={Shield}",
            combatId, casterId, casterName, targetId, targetName, buffId, buffName, stacks, duration, shield);
    }

    public static void BuffExpire(ILogger logger, long combatId, long entityId, string entityName, int buffId, string buffName)
    {
        logger.LogDebug(
            "[CombatTrace] event=buff_expire | combat={Combat} | entity={Eid}({Name}) | buff={Bid}({BName})",
            combatId, entityId, entityName, buffId, buffName);
    }

    public static void BuffTick(ILogger logger, long combatId, long targetId, string targetName,
        int buffId, string buffName, int tickDamage, string damageType, int tickCount)
    {
        logger.LogDebug(
            "[CombatTrace] event=buff_tick | combat={Combat} | target={Tid}({TName}) | buff={Bid}({BName}) | dmg={Dmg} | type={Type} | tick={Tick}",
            combatId, targetId, targetName, buffId, buffName, tickDamage, damageType, tickCount);
    }

    public static void BuffPurify(ILogger logger, long combatId, long casterId, string casterName,
        long targetId, string targetName, int removedCount)
    {
        logger.LogDebug(
            "[CombatTrace] event=buff_purify | combat={Combat} | caster={Cid}({CName}) | target={Tid}({TName}) | removed={Count}",
            combatId, casterId, casterName, targetId, targetName, removedCount);
    }

    public static void BuffShieldAbsorb(ILogger logger, long combatId, long targetId, string targetName,
        int damage, int absorbed, int shieldRemaining)
    {
        logger.LogDebug(
            "[CombatTrace] event=buff_shield_absorb | combat={Combat} | target={Tid}({TName}) | incoming_dmg={Dmg} | absorbed={Absorbed} | shield_remaining={Remaining}",
            combatId, targetId, targetName, damage, absorbed, shieldRemaining);
    }

    // ---- 死亡 ----

    public static void Death(ILogger logger, long combatId, long entityId, string entityName, int hpBeforeDeath)
    {
        logger.LogInformation(
            "[CombatTrace] event=death | combat={Combat} | entity={Eid}({Name}) | hp_before={Hp}",
            combatId, entityId, entityName, hpBeforeDeath);
    }

    // ---- 脱战 ----

    public static void Disengage(ILogger logger, long combatId, long entityId, string entityName, string reason)
    {
        logger.LogInformation(
            "[CombatTrace] event=disengage | combat={Combat} | entity={Eid}({Name}) | reason={Reason}",
            combatId, entityId, entityName, reason);
    }

    // ---- MP 恢复 ----

    public static void MpRegen(ILogger logger, long combatId, long entityId, string entityName, int regen, int mpAfter, bool inCombat)
    {
        logger.LogDebug(
            "[CombatTrace] event=mp_regen | combat={Combat} | entity={Eid}({Name}) | regen={Regen} | mp_after={MpAfter} | in_combat={InCombat}",
            combatId, entityId, entityName, regen, mpAfter, inCombat);
    }

    // ---- HP 恢复 ----

    public static void HpRegen(ILogger logger, long entityId, string entityName, int regen, int hpAfter)
    {
        logger.LogDebug(
            "[CombatTrace] event=hp_regen | entity={Eid}({Name}) | regen={Regen} | hp_after={HpAfter}",
            entityId, entityName, regen, hpAfter);
    }

    // ---- 属性快照 ----

    public static void AttrSnapshot(ILogger logger, long combatId, long entityId, string entityName,
        int patk, int matk, int pdef, int mdef,
        int buffPatk, int buffMatk, int buffPdef, int buffMdef)
    {
        logger.LogDebug(
            "[CombatTrace] event=attr_snapshot | combat={Combat} | entity={Eid}({Name}) | patk={Patk}+{BPatk} | matk={Matk}+{BMatk} | pdef={Pdef}+{BPdef} | mdef={Mdef}+{BMdef}",
            combatId, entityId, entityName, patk, buffPatk, matk, buffMatk, pdef, buffPdef, mdef, buffMdef);
    }
}