using GameServer.Common.Net;
using GameServer.Services.Core;
using Google.Protobuf;
using PCommon = global::Common;
using PGame = global::Game;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)global::Protocol.MessageId.GameUpdateUiPanelPosReq)]
public class UpdateUIPanelPosHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;

    public UpdateUIPanelPosHandler(PlayerSessionManager session) => _session = session;

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return MakeError(PCommon.ErrorCode.Unauthorized);

        var req = PGame.UpdateUIPanelPosRequest.Parser.ParseFrom(data);
        await _session.Roles.UpdateUIPanelPos(player.RoleId,
            (int)req.PosX, (int)req.PosY, (int)req.Width, (int)req.Height);

        return new PGame.UpdateUIPanelPosResponse { Code = PCommon.ErrorCode.Success }.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code)
        => new PCommon.Response { Code = code, Message = "" }.ToByteArray();
}
