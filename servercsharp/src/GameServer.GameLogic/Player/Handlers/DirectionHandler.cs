using GameServer.Common;
using GameServer.Common.Net;
using GameServer.Services.Core;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

/// <summary>
/// 朝向变更处理器 — 客户端不移动但改变方向时发送
/// </summary>
[HandlesMessage((int)PProtocol.MessageId.GameDirectionChangeReq)]
public class DirectionChangeHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly ILogger<DirectionChangeHandler> _logger;

    public DirectionChangeHandler(PlayerSessionManager session, ILogger<DirectionChangeHandler> logger)
    {
        _session = session;
        _logger = logger;
    }

    public Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.DirectionNotify.Parser.ParseFrom(data);
        int direction = req.Direction;

        if (direction < 0 || direction > 3)
            return Task.FromResult<byte[]?>(null);

        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return Task.FromResult<byte[]?>(null);

        player.Direction = direction;

        // 广播给地图上所有其他玩家
        var (mapName, _) = _session.MapService.World.FindEntityPosition(claims.AccountId);
        if (mapName != null)
        {
            var notify = new PGame.DirectionNotify
            {
                EntityId = (ulong)claims.AccountId,
                Direction = direction,
            };
            _session.MapService.BroadcastToMap(mapName, (int)PProtocol.MessageId.GameDirectionNotify, notify.ToByteArray());
        }

        return Task.FromResult<byte[]?>(null);
    }
}