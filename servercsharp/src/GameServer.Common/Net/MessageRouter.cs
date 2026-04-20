namespace GameServer.Common.Net;

/// <summary>
/// 消息上下文 — Gateway 转发业务消息时携带的连接信息
/// </summary>
public class MessageContext
{
    public required long ConnId { get; init; }
    public required uint Session { get; init; }
    public required string Token { get; init; }
    public required long AccountId { get; init; }
    public required int ServerId { get; init; }

    /// <summary>由 AuthMiddleware 填充的已验证 claims</summary>
    public GameServer.Common.Security.GatewayTokenClaims? Claims { get; set; }
}

/// <summary>
/// 消息路由 — msg_id 到 handler 的映射
/// </summary>
public class MessageRouter
{
    private readonly Dictionary<int, Func<MessageContext, byte[], Task<byte[]?>>> _handlers = new();

    public void Register(int msgId, Func<MessageContext, byte[], Task<byte[]?>> handler)
    {
        _handlers[msgId] = handler;
    }

    public async Task<byte[]?> Dispatch(int msgId, MessageContext ctx, byte[] data)
    {
        if (_handlers.TryGetValue(msgId, out var handler))
            return await handler(ctx, data);
        return null;
    }

    public bool HasRoute(int msgId) => _handlers.ContainsKey(msgId);
}
