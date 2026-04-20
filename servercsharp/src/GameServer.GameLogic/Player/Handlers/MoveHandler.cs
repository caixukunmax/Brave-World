using GameServer.Common.Net;
using GameServer.Services.Core;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameMoveReq)]
public class MoveStartHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;
    private readonly ILogger<MoveStartHandler> _logger;

    public MoveStartHandler(PlayerSessionManager session, INetworkSender network, ILogger<MoveStartHandler> logger)
    {
        _session = session;
        _network = network;
        _logger = logger;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.MoveRequest.Parser.ParseFrom(data);
        int fromX = (int)req.FromX, fromY = (int)req.FromY;
        int toX = (int)req.ToX, toY = (int)req.ToY;
        var mapName = req.MapName;

        if (Math.Abs(toX - fromX) + Math.Abs(toY - fromY) != 1)
            return MoveRsp(PCommon.ErrorCode.InvalidRequest, "invalid distance", fromX, fromY);

        if (string.IsNullOrEmpty(mapName) || !_session.MapService.IsWalkable(mapName, toX, toY))
            return MoveRsp(PCommon.ErrorCode.Forbidden, "target not walkable", fromX, fromY);

        // 蓄力期间禁止移动
        if (_session.CombatService?.IsCasting(claims.AccountId) == true)
            return MoveRsp(PCommon.ErrorCode.Forbidden, "casting", fromX, fromY);

        // 获取玩家移动速度
        int durationMs = GameConstants.BaseMoveSpeedMs;
        if (_session.TryGetPlayer(claims.AccountId, out var player))
        {
            durationMs = player.MoveSpeedMs > 0 ? player.MoveSpeedMs : GameConstants.BaseMoveSpeedMs;
        }

        // 预占目标格
        bool reserved = _session.MapService.World.TryReserveMove(
            claims.AccountId, mapName, fromX, fromY, toX, toY,
            durationMs, GameConstants.MoveCheckRatio,
            GameConstants.MoveDualGridStartRatio, GameConstants.MoveDualGridEndRatio);

        if (!reserved)
        {
            // 目标格被占据（怪物/其他玩家/预占），检查碰撞触发战斗
            _logger.LogWarning("[Move] reserve failed: player={PlayerId} from=({FX},{FY}) to=({TX},{TY}) — checking collision at from pos",
                claims.AccountId, fromX, fromY, toX, toY);
            _session.MapService.CheckEntityCollision(claims.AccountId, mapName, fromX, fromY);
            return MoveRsp(PCommon.ErrorCode.Success, "attack", fromX, fromY);
        }

        return MoveRsp(PCommon.ErrorCode.Success, "", toX, toY, durationMs);
    }

    private static byte[] MoveRsp(PCommon.ErrorCode code, string msg, int x, int y, int durationMs = 0)
    {
        var rsp = new PGame.MoveResponse
        {
            Code = code,
            Message = msg,
            X = x,
            Y = y,
        };
        if (durationMs > 0)
        {
            rsp.DurationMs = durationMs;
            rsp.CheckRatio = GameConstants.MoveCheckRatio;
            rsp.DualStartRatio = GameConstants.MoveDualGridStartRatio;
            rsp.DualEndRatio = GameConstants.MoveDualGridEndRatio;
        }
        return rsp.ToByteArray();
    }
}

[HandlesMessage((int)PProtocol.MessageId.GameMoveConfirmReq)]
public class MoveConfirmHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;
    private readonly ILogger<MoveConfirmHandler> _logger;

    public MoveConfirmHandler(PlayerSessionManager session, INetworkSender network, ILogger<MoveConfirmHandler> logger)
    {
        _session = session;
        _network = network;
        _logger = logger;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.MoveConfirmRequest.Parser.ParseFrom(data);

        bool ok = _session.MapService.World.ConfirmMove(claims.AccountId);
        if (!ok)
        {
            // 确认失败，通知客户端回退
            var res = _session.MapService.World.GetReservation(claims.AccountId);
            if (res != null)
            {
                var notify = new PGame.MoveCancelNotify
                {
                    EntityId = (ulong)claims.AccountId,
                    RollbackX = res.FromX,
                    RollbackY = res.FromY,
                };
                _network.SendToAccount(claims.AccountId, claims.ServerId,
                    (int)PProtocol.MessageId.GameMoveCancelNotify, notify.ToByteArray());
            }
            return null;
        }

        // Confirm 成功：坐标已更新到目标格，检查相邻敌方实体
        var res2 = _session.MapService.World.GetReservation(claims.AccountId);
        if (res2 != null)
        {
            var mapName = _session.MapService.World.GetEntityMapName(claims.AccountId);
            if (mapName != null)
            {
                _logger.LogInformation("[MoveConfirm] player={PlayerId} confirmed at ({TX},{TY}) map={Map} — checking collision",
                    claims.AccountId, res2.TargetX, res2.TargetY, mapName);
                _session.MapService.CheckEntityCollision(claims.AccountId, mapName, res2.TargetX, res2.TargetY);
            }
        }

        return null;
    }
}

[HandlesMessage((int)PProtocol.MessageId.GameMoveCompleteReq)]
public class MoveCompleteHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;

    public MoveCompleteHandler(PlayerSessionManager session) => _session = session;

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.MoveCompleteRequest.Parser.ParseFrom(data);

        _session.MapService.World.CompleteMove(claims.AccountId);

        // 更新数据库中的坐标
        if (_session.TryGetPlayer(claims.AccountId, out var player))
        {
            player.GridX = req.TargetX;
            player.GridY = req.TargetY;
        }

        return null;
    }
}
