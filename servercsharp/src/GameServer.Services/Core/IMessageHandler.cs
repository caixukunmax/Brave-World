using GameServer.Common.Net;

namespace GameServer.Services.Core;

/// <summary>
/// 消息处理器接口 — 每个消息类型实现一个 Handler
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class HandlesMessageAttribute : Attribute
{
    public int MessageId { get; }
    public HandlesMessageAttribute(int messageId) => MessageId = messageId;
}

public interface IMessageHandler
{
    Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data);
}
