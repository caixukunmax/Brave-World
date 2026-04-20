using GameServer.Common.Net;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Core;

/// <summary>
/// 消息处理器注册表 — 手动注册 IMessageHandler 到路由
/// </summary>
public class MessageHandlerRegistry
{
    private readonly ILogger<MessageHandlerRegistry> _logger;
    private readonly AuthMiddleware _auth;
    private readonly List<(int msgId, IMessageHandler handler)> _registrations = new();

    public MessageHandlerRegistry(ILogger<MessageHandlerRegistry> logger, AuthMiddleware auth)
    {
        _logger = logger;
        _auth = auth;
    }

    /// <summary>
    /// 添加一个 handler 及其对应的 msgId
    /// </summary>
    public void Add(int msgId, IMessageHandler handler)
    {
        _registrations.Add((msgId, handler));
    }

    /// <summary>
    /// 清除所有已注册的 handler（热更前调用）
    /// </summary>
    public void Clear()
    {
        _registrations.Clear();
    }

    /// <summary>
    /// 注册所有已添加的 handler 到 MessageRouter
    /// </summary>
    public void RegisterAll(MessageRouter router)
    {
        foreach (var (msgId, handler) in _registrations)
        {
            var handlerInstance = handler; // capture

            router.Register(msgId, async (ctx, data) =>
            {
                // 统一 auth 检查
                var claims = _auth.Validate(ctx);
                if (claims == null)
                    return AuthMiddleware.MakeUnauthorized();

                // 注入 claims 到 context
                ctx.Claims = claims;
                return await handlerInstance.HandleAsync(ctx, data);
            });

            _logger.LogInformation("Registered handler: {Handler} for msgId={MsgId}", handler.GetType().Name, msgId);
        }
    }
}
