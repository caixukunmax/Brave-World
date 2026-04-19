namespace GameServer.Common.Events;

/// <summary>
/// 进程内事件总线 — 替代 Skynet multicast channel
/// </summary>
public class EventBus
{
    private readonly Dictionary<string, List<Action<object?>>> _handlers = new();

    public void On(string eventName, Action<object?> handler)
    {
        if (!_handlers.TryGetValue(eventName, out var list))
        {
            list = new List<Action<object?>>();
            _handlers[eventName] = list;
        }
        list.Add(handler);
    }

    public void Emit(string eventName, object? data = null)
    {
        if (!_handlers.TryGetValue(eventName, out var list)) return;
        foreach (var handler in list)
        {
            try { handler(data); }
            catch (Exception ex)
            {
                Console.WriteLine($"[EventBus] handler error for {eventName}: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// 事件类型常量
/// </summary>
public static class EventTypes
{
    public const string PlayerLogin = "player_login";
    public const string PlayerLogout = "player_logout";
    public const string PlayerKick = "player_kick";
    public const string RoleCreate = "role_create";
    public const string RoleLevelUp = "role_levelup";
    public const string ConnectionOpen = "connection_open";
    public const string ConnectionClose = "connection_close";
}
