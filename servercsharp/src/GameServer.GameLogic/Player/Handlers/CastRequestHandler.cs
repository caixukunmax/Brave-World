using GameServer.Common.Net;
using GameServer.Services.Core;
using GameServer.Services.Player;
using Google.Protobuf;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameCastReq)]
public class CastRequestHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;

    public CastRequestHandler(PlayerSessionManager session)
    {
        _session = session;
    }

    public Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
        {
            return Task.FromResult<byte[]?>(MakeError("unauthorized"));
        }

        var req = PGame.CastRequest.Parser.ParseFrom(data);
        int skillId = (int)req.SkillId;
        long? targetId = req.TargetId > 0 ? (long)req.TargetId : null;

        if (skillId <= 0)
        {
            return Task.FromResult<byte[]?>(MakeError("invalid_skill"));
        }

        var combatService = _session.CombatService;
        if (combatService == null)
        {
            return Task.FromResult<byte[]?>(MakeError("combat_not_ready"));
        }

        var maps = _session.MapService.GetMapsSnapshot();
        _session.MapService.RefreshCombatPositionsForSnapshot(maps);
        var result = combatService.HandleCastRequest(claims.AccountId, skillId, targetId, maps);
        return Task.FromResult(result);
    }

    private static byte[] MakeError(string error)
        => new PGame.CastResponse { Success = false, Error = error }.ToByteArray();
}
