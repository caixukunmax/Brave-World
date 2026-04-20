using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Combat.Actions;

/// <summary>
/// 打断施法 Action — 将目标从 CASTING 状态重置
/// </summary>
public class InterruptCastAction : ICombatAction
{
    private readonly ILogger _logger;

    public InterruptCastAction(ILogger<InterruptCastAction> logger)
    {
        _logger = logger;
    }

    public string ActionType => "InterruptCast";

    public ActionResult Execute(long casterId, List<long> targets, ActionContext context)
    {
        if (context.CombatManager == null)
            return new ActionResult { Success = false };

        foreach (var targetId in targets)
        {
            var targetCtx = context.CombatManager.RelationsMgr.Contexts.GetValueOrDefault(targetId);
            if (targetCtx != null && targetCtx.SubState == "CASTING")
            {
                targetCtx.SubState = "NONE";
                targetCtx.CastSkillId = null;
                targetCtx.CastEndTime = null;
                _logger.LogInformation("[InterruptCast] target={TargetId} interrupted by caster={CasterId}", targetId, casterId);
            }
        }

        return new ActionResult { Success = true };
    }
}
