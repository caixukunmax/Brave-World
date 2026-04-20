namespace GameServer.Services.Map.Combat.Actions;

/// <summary>
/// 技能 Action 接口 — 移植自 combat/actions/*.lua
/// </summary>
public interface ICombatAction
{
    string ActionType { get; }
    ActionResult Execute(long casterId, List<long> targets, ActionContext context);
}

public class ActionContext
{
    public int SkillId { get; set; }
    public int SkillLevel { get; set; }
    public Dictionary<string, object>? ActionParams { get; set; }
    public CombatManager? CombatManager { get; set; }
    public Dictionary<string, MapState>? Maps { get; set; }
}

public class ActionResult
{
    public bool Success { get; set; }
    public List<DamageResult> Results { get; set; } = new();
}

public class DamageResult
{
    public long TargetId { get; set; }
    public int Damage { get; set; }
    public string DamageType { get; set; } = "physical";
}
