namespace GameServer.Services.Gateway;

/// <summary>
/// 单个客户端连接状态 — 对应 gateway/service.lua connections[fd]
/// </summary>
public class Connection
{
    public long Id { get; init; }
    public System.Net.Sockets.Socket Socket { get; init; } = null!;
    public string Address { get; set; } = "";
    public string Token { get; set; } = "";
    public long AccountId { get; set; }
    public int ServerId { get; set; }
    public long ConnId { get; init; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public CancellationTokenSource Cts { get; } = new();
}
