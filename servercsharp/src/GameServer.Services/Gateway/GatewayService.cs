using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using GameServer.Common.Net;
using GameServer.Services.Core;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PCommon = global::Common;
using PGateway = global::Gateway;
using PProtocol = global::Protocol;

namespace GameServer.Services.Gateway;

/// <summary>
/// TCP 网关服务 — 移植自 gateway/service.lua
/// 双通道设计：
/// 1. ConnectionChannel 串行处理 socket 生命周期、账号绑定、心跳、发包写操作；
/// 2. GameLoopScheduler 串行执行业务 handler，避免 DB/重逻辑阻塞 I/O。
/// </summary>
public class GatewayService : INetworkSender
{
    private readonly ILogger<GatewayService> _logger;
    private readonly MessageRouter _router;
    private readonly IGameLoopScheduler _gameLoop;
    private readonly int _port;
    private readonly int _heartbeatTimeoutSeconds;

    private long _connCounter;
    private readonly Dictionary<long, Connection> _connections = new();
    private readonly Dictionary<string, long> _accountConnections = new();
    private readonly Channel<Func<Task>> _connectionChannel = Channel.CreateUnbounded<Func<Task>>();

    public GatewayService(
        ILogger<GatewayService> logger,
        MessageRouter router,
        IGameLoopScheduler gameLoop,
        int port = 8889,
        int heartbeatTimeoutSeconds = 3600)
    {
        _logger = logger;
        _router = router;
        _gameLoop = gameLoop;
        _port = port;
        _heartbeatTimeoutSeconds = heartbeatTimeoutSeconds;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _ = Task.Run(() => ProcessConnectionActionsAsync(ct), ct);
        _ = Task.Run(() => HeartbeatCheckLoop(ct), ct);

        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Any, _port));
        listener.Listen(100);
        _logger.LogInformation("Gateway listening on 0.0.0.0:{Port}", _port);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var socket = await listener.AcceptAsync(ct);
                var connId = Interlocked.Increment(ref _connCounter);
                var addr = socket.RemoteEndPoint?.ToString() ?? "?";

                var conn = new Connection
                {
                    Id = connId,
                    Socket = socket,
                    Address = addr,
                    ConnId = connId,
                    LastHeartbeat = DateTime.UtcNow,
                };

                _connectionChannel.Writer.TryWrite(() =>
                {
                    _connections[connId] = conn;
                    _logger.LogInformation("New connection: connId={ConnId} addr={Addr}", connId, addr);
                    return Task.CompletedTask;
                });

                _ = HandleConnectionAsync(conn, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Accept error"); }
        }
    }

    public void SendToClient(long connId, int msgId, uint session, byte[] data)
    {
        _connectionChannel.Writer.TryWrite(async () =>
        {
            if (!_connections.TryGetValue(connId, out var conn)) return;
            if (!conn.Socket.Connected)
            {
                DoCloseConnection(connId, "inactive");
                return;
            }
            await SendPacketAsync(conn, msgId, session, data);
        });
    }

    public void SendToAccount(long accountId, int serverId, int msgId, byte[] data)
    {
        _connectionChannel.Writer.TryWrite(async () =>
        {
            var key = $"{accountId}:{serverId}";
            if (!_accountConnections.TryGetValue(key, out var connId)) return;
            if (!_connections.TryGetValue(connId, out var conn)) return;
            if (!conn.Socket.Connected)
            {
                DoCloseConnection(connId, "inactive");
                return;
            }
            await SendPacketAsync(conn, msgId, 0, data);
        });
    }

    public void BindToken(long connId, string token, long accountId, int serverId)
    {
        _connectionChannel.Writer.TryWrite(async () =>
        {
            if (!_connections.TryGetValue(connId, out var conn)) return;

            if (accountId > 0 && serverId > 0)
            {
                var key = $"{accountId}:{serverId}";
                if (_accountConnections.TryGetValue(key, out var oldConnId) && oldConnId != connId)
                {
                    if (_connections.TryGetValue(oldConnId, out var oldConn))
                    {
                        _logger.LogInformation("Kick old connection: connId={OldConnId} (new={NewConnId})", oldConnId, connId);
                        var notifyData = new PGateway.DisconnectNotify { Reason = "account_kick" }.ToByteArray();
                        await SendPacketAsync(oldConn, (int)PProtocol.MessageId.GatewayKickNotify, 0, notifyData);
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(500);
                            CloseConnection(oldConnId, "kick_replace");
                        });
                    }
                }
                _accountConnections[key] = connId;
            }

            conn.Token = token;
            conn.AccountId = accountId;
            conn.ServerId = serverId;
        });
    }

    public int GetOnlineCount() => _connections.Count;

    // ---- Internal ----

    private async Task HandleConnectionAsync(Connection conn, CancellationToken ct)
    {
        var buffer = new byte[PacketCodec.MaxPacketSize + 4];
        int offset = 0;

        try
        {
            while (!ct.IsCancellationRequested && !conn.Cts.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await conn.Socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), SocketFlags.None);
                    if (bytesRead == 0) break;
                }
                catch (SocketException) { break; }

                offset += bytesRead;

                while (offset >= 4)
                {
                    var result = PacketCodec.TryDecode(buffer, offset);
                    if (result == null) break;

                    var (packet, consumed) = result.Value;
                    offset -= consumed;
                    if (offset > 0)
                        Buffer.BlockCopy(buffer, consumed, buffer, 0, offset);

                    // 所有数据包先回到连接通道：更新心跳、按类型分发
                    _connectionChannel.Writer.TryWrite(() => HandleIncomingPacket(conn, packet));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Connection error: connId={ConnId}", conn.ConnId); }
        finally { CloseConnection(conn.Id, "connection_lost"); }
    }

    private Task HandleIncomingPacket(Connection conn, PCommon.Packet packet)
    {
        conn.LastHeartbeat = DateTime.UtcNow;
        var msgId = (int)packet.MsgId;

        // 系统包（连接/心跳）在连接通道直接处理，避免进入游戏逻辑调度器
        if (msgId == (int)PProtocol.MessageId.GatewayConnectReq)
            return HandleConnectReq(conn, packet.Session, packet.Data.ToByteArray());

        if (msgId == (int)PProtocol.MessageId.GatewayHeartbeatReq)
            return HandleHeartbeatReq(conn);

        // 业务包：捕获连接上下文后投递到游戏逻辑调度器执行
        var ctx = new MessageContext
        {
            ConnId = conn.ConnId,
            Session = packet.Session,
            Token = conn.Token,
            AccountId = conn.AccountId,
            ServerId = conn.ServerId,
        };
        var data = packet.Data.ToByteArray();

        _gameLoop.Enqueue(async () =>
        {
            _logger.LogDebug("RECV connId={ConnId} msgId={MsgId} session={Session}", conn.ConnId, msgId, packet.Session);

            if (!_router.HasRoute(msgId))
            {
                _logger.LogWarning("No route for msgId={MsgId}", msgId);
                var errResp = new PCommon.Response { Code = PCommon.ErrorCode.ServiceUnavailable, Message = $"No route for msg_id={msgId}" }.ToByteArray();
                SendToClient(conn.ConnId, msgId + 1, packet.Session, errResp);
                return;
            }

            var responseData = await _router.Dispatch(msgId, ctx, data);
            if (responseData != null)
                SendToClient(conn.ConnId, msgId + 1, packet.Session, responseData);
        });

        return Task.CompletedTask;
    }

    private async Task HandleConnectReq(Connection conn, uint session, byte[] data)
    {
        var req = PGateway.ConnectRequest.Parser.ParseFrom(data);
        if (req.ClientInfo != null)
            conn.Address = req.ClientInfo.Ip ?? conn.Address;

        var rsp = new PGateway.ConnectResponse
        {
            Code = 0,
            Message = "",
            ConnId = (uint)conn.ConnId,
            ServerTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        await SendPacketAsync(conn, (int)PProtocol.MessageId.GatewayConnectRsp, session, rsp.ToByteArray());
    }

    private async Task HandleHeartbeatReq(Connection conn)
    {
        var rsp = new PGateway.HeartbeatResponse
        {
            ServerTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            OnlineCount = (uint)_connections.Count,
        };
        await SendPacketAsync(conn, (int)PProtocol.MessageId.GatewayHeartbeatRsp, 0, rsp.ToByteArray());
    }

    private async Task SendPacketAsync(Connection conn, int msgId, uint session, byte[] data)
    {
        try
        {
            if (!conn.Socket.Connected) return;
            var packet = PacketCodec.MakePacket(msgId, session, data);
            var bytes = PacketCodec.Encode(packet);
            await conn.Socket.SendAsync(new ArraySegment<byte>(bytes), SocketFlags.None);
        }
        catch (SocketException)
        {
            // 发送失败 = 连接已死，立即同步清理，避免后续消息继续尝试
            DoCloseConnection(conn.ConnId, "send_failed");
        }
        catch (ObjectDisposedException)
        {
            DoCloseConnection(conn.ConnId, "disposed");
        }
        catch (Exception ex) { _logger.LogError(ex, "Send failed: connId={ConnId}", conn.ConnId); }
    }

    /// <summary>同步执行连接清理（仅在 _connectionChannel 处理线程内调用）</summary>
    private void DoCloseConnection(long connId, string reason)
    {
        if (_connections.Remove(connId, out var conn))
        {
            if (conn.AccountId > 0 && conn.ServerId > 0)
            {
                var key = $"{conn.AccountId}:{conn.ServerId}";
                if (_accountConnections.TryGetValue(key, out var existingId) && existingId == connId)
                    _accountConnections.Remove(key);
            }
            _logger.LogInformation("Connection closed: connId={ConnId} reason={Reason}", connId, reason);
            try { conn.Socket.Close(); } catch { }
            conn.Cts.Cancel();
        }
    }

    private void CloseConnection(long connId, string reason)
    {
        _connectionChannel.Writer.TryWrite(() =>
        {
            DoCloseConnection(connId, reason);
            return Task.CompletedTask;
        });
    }

    private async Task ProcessConnectionActionsAsync(CancellationToken ct)
    {
        await foreach (var action in _connectionChannel.Reader.ReadAllAsync(ct))
        {
            try { await action(); }
            catch (Exception ex) { _logger.LogError(ex, "Connection action error"); }
        }
    }

    private async Task HeartbeatCheckLoop(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(GameConstants.HeartbeatCheckSec));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var now = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(_heartbeatTimeoutSeconds);
            var toClose = new List<long>();

            _connectionChannel.Writer.TryWrite(() =>
            {
                foreach (var (connId, conn) in _connections)
                {
                    if (now - conn.LastHeartbeat > timeout)
                    {
                        _logger.LogInformation("Heartbeat timeout: connId={ConnId}", connId);
                        toClose.Add(connId);
                    }
                }
                foreach (var connId in toClose)
                    CloseConnection(connId, "heartbeat_timeout");
                return Task.CompletedTask;
            });
        }
    }
}
