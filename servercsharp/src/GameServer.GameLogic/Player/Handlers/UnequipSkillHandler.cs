using GameServer.Common.Net;
using GameServer.Services.Core;
using Google.Protobuf;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameUnequipSkillReq)]
public class UnequipSkillHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;

    public UnequipSkillHandler(PlayerSessionManager session, INetworkSender network)
    {
        _session = session;
        _network = network;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return MakeError(PCommon.ErrorCode.Unauthorized);

        var req = PGame.UnequipSkillRequest.Parser.ParseFrom(data);
        int slotIndex = (int)req.SlotIndex;

        if (slotIndex < 0 || slotIndex >= 4)
            return MakeError(PCommon.ErrorCode.InvalidRequest, "invalid slot (0-3)");

        // 扩展列表到 slotIndex 长度
        while (player.EquippedSkills.Count <= slotIndex)
            player.EquippedSkills.Add(0);

        player.EquippedSkills[slotIndex] = 0;

        // 持久化
        await _session.Roles.Update(player.RoleId, u => u.Set(r => r.EquippedSkills, player.EquippedSkills));

        // 更新内存 MapPlayerState
        var mapPlayer = _session.MapService.GetPlayerOnMap(player.CurrentMap, claims.AccountId);
        if (mapPlayer != null)
            mapPlayer.EquippedSkills = new List<int>(player.EquippedSkills);

        // 推送 FullRoleInfo
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var roleInfo = PlayerProtoMapper.BuildRoleInfo(player, now);
        _network.SendToAccount(claims.AccountId, claims.ServerId, (int)PProtocol.MessageId.GameRoleAttrNotify, roleInfo.ToByteArray());

        var rsp = new PGame.UnequipSkillResponse { Code = PCommon.ErrorCode.Success, Message = "" };
        foreach (var sid in player.EquippedSkills) rsp.EquippedSkills.Add((uint)sid);
        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code, string msg = "")
        => new PGame.UnequipSkillResponse { Code = code, Message = msg }.ToByteArray();
}
