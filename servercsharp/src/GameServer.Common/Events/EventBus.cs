using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GameServer.Common.Events;

/// <summary>
/// 进程内事件总线 — 替代 Skynet multicast channel
/// 线程安全：订阅与发布可并发执行，发布时读取 handler 列表快照。
/// </summary>
public class EventBus
{
    private readonly ILogger<EventBus> _logger;
    private readonly ConcurrentDictionary<string, List<Action<object?>>> _handlers = new();

    public EventBus(ILogger<EventBus> logger)
    {
        _logger = logger;
    }

    public void On(string eventName, Action<object?> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var list = _handlers.GetOrAdd(eventName, _ => new List<Action<object?>>());
        lock (list)
        {
            list.Add(handler);
        }
    }

    public void Off(string eventName, Action<object?> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryGetValue(eventName, out var list)) return;
        lock (list)
        {
            list.Remove(handler);
        }
    }

    public void Emit(string eventName, object? data = null)
    {
        if (!_handlers.TryGetValue(eventName, out var list)) return;

        Action<object?>[] snapshot;
        lock (list)
        {
            snapshot = list.Count == 0 ? Array.Empty<Action<object?>>() : list.ToArray();
        }

        foreach (var handler in snapshot)
        {
            try
            {
                handler(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventBus] handler error for {EventName}", eventName);
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
