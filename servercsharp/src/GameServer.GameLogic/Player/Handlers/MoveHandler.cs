using GameServer.Common.Net;
using GameServer.Services.Core;
using GameServer.Services.World;
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

    public Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.MoveRequest.Parser.ParseFrom(data);
        int fromX = (int)req.FromX, fromY = (int)req.FromY;
        int toX = (int)req.ToX, toY = (int)req.ToY;
        var mapName = req.MapName;

        if (Math.Abs(toX - fromX) + Math.Abs(toY - fromY) != 1)
            return Task.FromResult<byte[]?>(MoveRsp(PCommon.ErrorCode.InvalidRequest, "invalid distance", fromX, fromY));

        if (string.IsNullOrEmpty(mapName) || !_session.MapService.IsWalkable(mapName, toX, toY))
            return Task.FromResult<byte[]?>(MoveRsp(PCommon.ErrorCode.Forbidden, "target not walkable", fromX, fromY));

        // 蓄力期间禁止移动
        if (_session.CombatService?.IsCasting(claims.AccountId) == true)
            return Task.FromResult<byte[]?>(MoveRsp(PCommon.ErrorCode.Forbidden, "casting", fromX, fromY));

        // 获取玩家移动速度（钳制在下限之上，防止过高移速导致闪现感）
        int durationMs = GameConstants.BaseMoveSpeedMs;
        if (_session.TryGetPlayer(claims.AccountId, out var player))
        {
            durationMs = player.MoveSpeedMs > 0 ? player.MoveSpeedMs : GameConstants.BaseMoveSpeedMs;
            // move_speed 属性值范围 120~600，值越大越快
            // 映射到实际移动时间：120→600ms（最慢），600→120ms（最快）
            durationMs = GameConstants.MinMoveSpeedMs + GameConstants.MaxMoveSpeedMs - durationMs;
            if (durationMs < GameConstants.MinMoveSpeedMs)
                durationMs = GameConstants.MinMoveSpeedMs;
            if (durationMs > GameConstants.MaxMoveSpeedMs)
                durationMs = GameConstants.MaxMoveSpeedMs;
        }

        // 地形减速：沙地0.7x、雪地0.6x、沼泽0.4x → durationMs 除以系数（变慢）
        float terrainRatio = _session.MapService.GetTerrainMoveSpeedRatio(mapName, toX, toY);
        if (terrainRatio > 0 && terrainRatio < 1.0f)
        {
            int oldDuration = durationMs;
            durationMs = (int)(durationMs / terrainRatio);
            _logger.LogInformation("[Move] terrain slowdown: player={PlayerId} ratio={Ratio} old={Old}ms new={New}ms",
                claims.AccountId, terrainRatio, oldDuration, durationMs);
        }

        // 预占目标格
        bool reserved = _session.MapService.World.TryReserveMove(
            claims.AccountId, mapName, fromX, fromY, toX, toY,
            durationMs, GameConstants.MoveCheckRatio,
            GameConstants.MoveDualGridStartRatio, GameConstants.MoveDualGridEndRatio);

        if (!reserved)
        {
            // 目标格被占据 — 尝试碰撞性移动（移动 30% 后再检测）
            bool collisionMove = _session.MapService.World.TryReserveCollisionMove(
                claims.AccountId, mapName, fromX, fromY, toX, toY,
                durationMs, GameConstants.MoveCheckRatio,
                GameConstants.MoveDualGridStartRatio, GameConstants.MoveDualGridEndRatio);

            if (collisionMove)
            {
                _logger.LogInformation("[Move] collision move: player={PlayerId} from=({FX},{FY}) to=({TX},{TY}) — deferring collision to 30%",
                    claims.AccountId, fromX, fromY, toX, toY);
                return Task.FromResult<byte[]?>(MoveRsp(PCommon.ErrorCode.Success, "", fromX, fromY, durationMs));
            }

            // 碰撞移动也失败（极少见），按原有逻辑处理
            return Task.FromResult<byte[]?>(MoveRsp(PCommon.ErrorCode.Forbidden, "blocked", fromX, fromY));
        }

        return Task.FromResult<byte[]?>(MoveRsp(PCommon.ErrorCode.Success, "", toX, toY, durationMs));
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

    public Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.MoveConfirmRequest.Parser.ParseFrom(data);

        var result = _session.MapService.World.ConfirmMoveEx(claims.AccountId);

        if (result == ConfirmResult.Collision)
        {
            // 碰撞：服务端校验目标格有敌人 → 弹回 + 开战
            var (mapName, pos) = _session.MapService.World.FindEntityPosition(claims.AccountId);
            if (mapName != null && pos != null)
            {
                _logger.LogInformation("[MoveConfirm] collision detected: player={PlayerId} at ({X},{Y}) map={Map}",
                    claims.AccountId, pos.Value.x, pos.Value.y, mapName);

                // 发送弹回通知
                var notify = new PGame.MoveCancelNotify
                {
                    EntityId = (ulong)claims.AccountId,
                    RollbackX = pos.Value.x,
                    RollbackY = pos.Value.y,
                };
                _network.SendToAccount(claims.AccountId, claims.ServerId,
                    (int)PProtocol.MessageId.GameMoveCancelNotify, notify.ToByteArray());

                // 触发战斗
                _session.MapService.CheckEntityCollision(claims.AccountId, mapName, pos.Value.x, pos.Value.y);
            }
            return Task.FromResult<byte[]?>(null);
        }

        if (result == ConfirmResult.Failed)
        {
            // 确认失败，通知客户端回退
            var res = _session.MapService.World.GetReservation(claims.AccountId);
            int rollbackX = res?.FromX ?? (int)req.TargetX;
            int rollbackY = res?.FromY ?? (int)req.TargetY;

            var notify = new PGame.MoveCancelNotify
            {
                EntityId = (ulong)claims.AccountId,
                RollbackX = rollbackX,
                RollbackY = rollbackY,
            };
            _network.SendToAccount(claims.AccountId, claims.ServerId,
                (int)PProtocol.MessageId.GameMoveCancelNotify, notify.ToByteArray());

            // 也检查碰撞（可能怪物移到了附近）
            var (mapName2, pos2) = _session.MapService.World.FindEntityPosition(claims.AccountId);
            if (mapName2 != null && pos2 != null)
                _session.MapService.CheckEntityCollision(claims.AccountId, mapName2, pos2.Value.x, pos2.Value.y);
            return Task.FromResult<byte[]?>(null);
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

        return Task.FromResult<byte[]?>(null);
    }
}

[HandlesMessage((int)PProtocol.MessageId.GameMoveCompleteReq)]
public class MoveCompleteHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly IDropService _dropService;

    public MoveCompleteHandler(PlayerSessionManager session, IDropService dropService)
    {
        _session = session;
        _dropService = dropService;
    }

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

        // 移动到目标格后尝试自动拾取掉落物
        var mapName = _session.MapService.World.GetEntityMapName(claims.AccountId);
        if (!string.IsNullOrEmpty(mapName))
        {
            await _dropService.TryAutoPickup(claims.AccountId, mapName, (int)req.TargetX, (int)req.TargetY);
        }

        return null;
    }
}

/// <summary>
/// 碰撞通知（客户端30%检测到敌人后直接发送）
/// 服务端校验并触发战斗，校验失败才发 MoveCancelNotify 强制回滚
/// </summary>
[HandlesMessage((int)PProtocol.MessageId.GameMoveCollisionNotify)]
public class MoveCollisionHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;
    private readonly ILogger<MoveCollisionHandler> _logger;

    public MoveCollisionHandler(PlayerSessionManager session, INetworkSender network, ILogger<MoveCollisionHandler> logger)
    {
        _session = session;
        _network = network;
        _logger = logger;
    }

    public Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.MoveCollisionNotify.Parser.ParseFrom(data);

        // 取消移动预约（清理 WorldState 中的占用）
        _session.MapService.World.CancelMove(claims.AccountId);

        // 找到玩家当前位置
        var (mapName, pos) = _session.MapService.World.FindEntityPosition(claims.AccountId);
        if (mapName == null || pos == null)
        {
            _logger.LogWarning("[MoveCollision] player={PlayerId} position not found", claims.AccountId);
            return Task.FromResult<byte[]?>(null);
        }

        // 校验：目标格是否真有敌人（防止作弊或过时信息）
        bool hasNearbyEnemy = _session.MapService.World.HasEnemyAt(mapName, req.TargetX, req.TargetY, claims.AccountId);

        if (!hasNearbyEnemy)
        {
            // 校验失败：目标格已无敌人，通知客户端强制回滚
            _logger.LogInformation("[MoveCollision] validation failed: no enemy at ({TX},{TY}) for player={PlayerId}",
                req.TargetX, req.TargetY, claims.AccountId);

            var notify = new PGame.MoveCancelNotify
            {
                EntityId = (ulong)claims.AccountId,
                RollbackX = pos.Value.x,
                RollbackY = pos.Value.y,
            };
            _network.SendToAccount(claims.AccountId, claims.ServerId,
                (int)PProtocol.MessageId.GameMoveCancelNotify, notify.ToByteArray());
            return Task.FromResult<byte[]?>(null);
        }

        // 校验通过：触发战斗
        _logger.LogInformation("[MoveCollision] validated: player={PlayerId} collided at ({TX},{TY}) map={Map}",
            claims.AccountId, req.TargetX, req.TargetY, mapName);
        _session.MapService.CheckEntityCollision(claims.AccountId, mapName, pos.Value.x, pos.Value.y);

        return Task.FromResult<byte[]?>(null);
    }
}
