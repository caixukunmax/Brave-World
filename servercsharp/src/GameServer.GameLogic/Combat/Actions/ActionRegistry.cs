namespace GameServer.Services.Map.Combat.Actions;

/// <summary>
/// Action 注册表 — 移植自 combat/actions/init.lua
/// </summary>
public class ActionRegistry
{
    private readonly Dictionary<string, ICombatAction> _actions = new();

    public void Register(ICombatAction action)
    {
        _actions[action.ActionType] = action;
    }

    public ICombatAction? Get(string type)
    {
        return _actions.GetValueOrDefault(type);
    }
}
