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
/// </summary>
public class GatewayService : INetworkSender
{
    private readonly ILogger<GatewayService> _logger;
    private readonly MessageRouter _router;
    private readonly int _port;
    private readonly int _heartbeatTimeoutSeconds;

    private long _connCounter;
    private readonly Dictionary<long, Connection> _connections = new();
    private readonly Dictionary<string, long> _accountConnections = new();
    private readonly Channel<Func<Task>> _actionChannel = Channel.CreateUnbounded<Func<Task>>();

    public GatewayService(ILogger<GatewayService> logger, MessageRouter router, int port = 8889, int heartbeatTimeoutSeconds = 3600)
    {
        _logger = logger;
        _router = router;
        _port = port;
        _heartbeatTimeoutSeconds = heartbeatTimeoutSeconds;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _ = Task.Run(() => ProcessActionsAsync(ct), ct);
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

                _actionChannel.Writer.TryWrite(() =>
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
        _actionChannel.Writer.TryWrite(async () =>
        {
            if (!_connections.TryGetValue(connId, out var conn)) return;
            await SendPacketAsync(conn, msgId, session, data);
        });
    }

    public void SendToAccount(long accountId, int serverId, int msgId, byte[] data)
    {
        _actionChannel.Writer.TryWrite(async () =>
        {
            var key = $"{accountId}:{serverId}";
            if (!_accountConnections.TryGetValue(key, out var connId)) return;
            if (!_connections.TryGetValue(connId, out var conn)) return;
            await SendPacketAsync(conn, msgId, 0, data);
        });
    }

    public void BindToken(long connId, string token, long accountId, int serverId)
    {
        _actionChannel.Writer.TryWrite(async () =>
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

                    _actionChannel.Writer.TryWrite(() => HandlePacket(conn, packet));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Connection error: connId={ConnId}", conn.ConnId); }
        finally { CloseConnection(conn.Id, "connection_lost"); }
    }

    private async Task HandlePacket(Connection conn, PCommon.Packet packet)
    {
        conn.LastHeartbeat = DateTime.UtcNow;
        var msgId = (int)packet.MsgId;
        var session = packet.Session;
        var data = packet.Data.ToByteArray();

        _logger.LogDebug("RECV connId={ConnId} msgId={MsgId} session={Session}", conn.ConnId, msgId, session);

        if (msgId == (int)PProtocol.MessageId.GatewayConnectReq)
        {
            await HandleConnectReq(conn, session, data);
            return;
        }

        if (msgId == (int)PProtocol.MessageId.GatewayHeartbeatReq)
        {
            await HandleHeartbeatReq(conn, session);
            return;
        }

        if (!_router.HasRoute(msgId))
        {
            _logger.LogWarning("No route for msgId={MsgId}", msgId);
            var errResp = new PCommon.Response { Code = PCommon.ErrorCode.ServiceUnavailable, Message = $"No route for msg_id={msgId}" }.ToByteArray();
            await SendPacketAsync(conn, msgId + 1, session, errResp);
            return;
        }

        var responseData = await _router.Dispatch(msgId, new MessageContext
        {
            ConnId = conn.ConnId,
            Session = session,
            Token = conn.Token,
            AccountId = conn.AccountId,
            ServerId = conn.ServerId,
        }, data);
        if (responseData != null)
            await SendPacketAsync(conn, msgId + 1, session, responseData);
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

    private async Task HandleHeartbeatReq(Connection conn, uint session)
    {
        var rsp = new PGateway.HeartbeatResponse
        {
            ServerTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            OnlineCount = (uint)_connections.Count,
        };
        await SendPacketAsync(conn, (int)PProtocol.MessageId.GatewayHeartbeatRsp, session, rsp.ToByteArray());
    }

    private async Task SendPacketAsync(Connection conn, int msgId, uint session, byte[] data)
    {
        try
        {
            var packet = PacketCodec.MakePacket(msgId, session, data);
            var bytes = PacketCodec.Encode(packet);
            await conn.Socket.SendAsync(new ArraySegment<byte>(bytes), SocketFlags.None);
        }
        catch (Exception ex) { _logger.LogError(ex, "Send failed: connId={ConnId}", conn.ConnId); }
    }

    private void CloseConnection(long connId, string reason)
    {
        _actionChannel.Writer.TryWrite(() =>
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
            return Task.CompletedTask;
        });
    }

    private async Task ProcessActionsAsync(CancellationToken ct)
    {
        await foreach (var action in _actionChannel.Reader.ReadAllAsync(ct))
        {
            try { await action(); }
            catch (Exception ex) { _logger.LogError(ex, "Action error"); }
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

            _actionChannel.Writer.TryWrite(() =>
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
