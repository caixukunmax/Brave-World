using GameServer.Common.Net;
using GameServer.Services.Core;
using Google.Protobuf;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameSetPreferredSkillReq)]
public class SetPreferredSkillHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;

    public SetPreferredSkillHandler(PlayerSessionManager session, INetworkSender network)
    {
        _session = session;
        _network = network;
    }

    public Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return Task.FromResult<byte[]?>(MakeError(PCommon.ErrorCode.Unauthorized));

        var req = PGame.SetPreferredSkillRequest.Parser.ParseFrom(data);
        int skillId = (int)req.SkillId;

        // skillId=0 表示取消优先
        if (skillId != 0)
        {
            // 验证技能已装备
            if (!player.EquippedSkills.Contains(skillId))
                return Task.FromResult<byte[]?>(MakeError(PCommon.ErrorCode.InvalidRequest, "skill not equipped"));
        }

        // 更新 MapPlayerState
        var mapPlayer = _session.MapService.GetPlayerOnMap(player.CurrentMap, claims.AccountId);
        if (mapPlayer != null)
        {
            mapPlayer.PreferredSkillId = skillId;
        }

        var rsp = new PGame.SetPreferredSkillResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "",
            PreferredSkillId = (uint)skillId,
        };
        return Task.FromResult<byte[]?>(rsp.ToByteArray());
    }

    private static byte[] MakeError(PCommon.ErrorCode code, string msg = "")
        => new PGame.SetPreferredSkillResponse { Code = code, Message = msg }.ToByteArray();
}
