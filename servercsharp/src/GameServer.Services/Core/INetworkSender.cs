namespace GameServer.Services.Core;

/// <summary>
/// 网络发送接口 — 解耦各服务对 GatewayService 的直接依赖
/// </summary>
public interface INetworkSender
{
    void SendToAccount(long accountId, int serverId, int msgId, byte[] data);
    void SendToClient(long connId, int msgId, uint session, byte[] data);
}
