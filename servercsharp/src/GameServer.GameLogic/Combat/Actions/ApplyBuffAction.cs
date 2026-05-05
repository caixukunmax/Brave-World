using GameServer.Common.Buffs;
using GameServer.Tables;

namespace GameServer.Services.Map.Combat.Actions;

/// <summary>
/// 施加 Buff 动作 — 技能效果之一
/// </summary>
public class ApplyBuffAction : ICombatAction
{
    private readonly LubanTableLoader? _tables;

    public ApplyBuffAction(LubanTableLoader? tables = null)
    {
        _tables = tables;
    }

    public string ActionType => "ApplyBuff";

    public ActionResult Execute(long casterId, List<long> targets, ActionContext context)
    {
        var result = new ActionResult { Success = true };
        if (context.CombatManager == null || context.ActionParams == null) return result;

        int buffId = context.ActionParams.TryGetValue("buff_id", out var bid) ? Convert.ToInt32(bid) : 0;
        int shieldBase = context.ActionParams.TryGetValue("shield_base", out var sb) ? Convert.ToInt32(sb) : 0;

        if (buffId == 0) return result;

        var now = Environment.TickCount64;
        var cfg = _tables?.GetBuff(buffId);
        long expireTime = cfg != null && cfg.Duration > 0 ? now + (long)(cfg.Duration * 1000) : 0;
        int shield = shieldBase > 0 ? shieldBase : (cfg?.ShieldBase ?? 0);

        foreach (var targetId in targets)
        {
            var targetCtx = context.CombatManager?.GetContext(targetId);
            if (targetCtx == null) continue;

            var buff = new BuffInstance
            {
                BuffId = buffId,
                Stacks = 1,
                ApplyTime = now,
                ExpireTime = expireTime,
                CasterId = casterId,
                TargetId = targetId,
                LastTickTime = now,
                ShieldRemaining = shield,
                SnapshotAtk = DealDamageAction.GetEntityAttr(casterId, "patk", context.Maps) ?? 10,
                SnapshotMatk = DealDamageAction.GetEntityAttr(casterId, "matk", context.Maps) ?? 10,
            };

            targetCtx.Buffs.AddBuff(buff);
            result.BuffAppliedTargets.Add(targetId);

            var combatId = context.CombatManager?.GetCombatId(casterId) ?? 0;
            var cmLogger = context.CombatManager?.Logger;
            if (cmLogger != null)
                CombatTrace.BuffApply(cmLogger, combatId, casterId, SkillPipeline.GetEntityName(casterId, context.Maps!),
                    targetId, SkillPipeline.GetEntityName(targetId, context.Maps!),
                    buffId, cfg?.Name ?? $"Buff{buffId}", 1, cfg?.Duration ?? 0, shield);
        }

        return result;
    }
}