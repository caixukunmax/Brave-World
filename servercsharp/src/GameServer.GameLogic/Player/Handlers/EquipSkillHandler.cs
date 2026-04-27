using GameServer.Common.Net;
using GameServer.Services.Core;
using GameServer.Services.Map.Combat;
using Google.Protobuf;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameEquipSkillReq)]
public class EquipSkillHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;

    public EquipSkillHandler(PlayerSessionManager session, INetworkSender network)
    {
        _session = session;
        _network = network;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return MakeError(PCommon.ErrorCode.Unauthorized);

        var req = PGame.EquipSkillRequest.Parser.ParseFrom(data);
        int skillId = (int)req.SkillId;
        int slotIndex = (int)req.SlotIndex;

        if (slotIndex < 0 || slotIndex >= 4)
            return MakeError(PCommon.ErrorCode.InvalidRequest, "invalid slot (0-3)");

        if (skillId <= 1)
            return MakeError(PCommon.ErrorCode.InvalidRequest, "cannot equip basic attack");

        if (SkillPipeline.GetSkillConfigStatic(skillId) == null)
            return MakeError(PCommon.ErrorCode.InvalidRequest, $"skill {skillId} not found");

        if (!player.LearnedSkills.Contains(skillId))
            return MakeError(PCommon.ErrorCode.InvalidRequest, "skill not learned");

        // 若已在其他槽位，交换
        int existingSlot = player.EquippedSkills.IndexOf(skillId);
        if (existingSlot >= 0)
        {
            int oldSkill = slotIndex < player.EquippedSkills.Count ? player.EquippedSkills[slotIndex] : 0;
            while (player.EquippedSkills.Count <= existingSlot)
                player.EquippedSkills.Add(0);
            player.EquippedSkills[existingSlot] = oldSkill;
        }

        // 扩展列表到 slotIndex 长度
        while (player.EquippedSkills.Count <= slotIndex)
            player.EquippedSkills.Add(0);

        player.EquippedSkills[slotIndex] = skillId;

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

        var rsp = new PGame.EquipSkillResponse { Code = PCommon.ErrorCode.Success, Message = "" };
        foreach (var sid in player.EquippedSkills) rsp.EquippedSkills.Add((uint)sid);
        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code, string msg = "")
        => new PGame.EquipSkillResponse { Code = code, Message = msg }.ToByteArray();
}
