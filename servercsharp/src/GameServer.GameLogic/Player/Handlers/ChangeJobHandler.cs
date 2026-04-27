using GameServer.Common.Net;
using GameServer.Database.Models;
using GameServer.Services.Core;
using GameServer.Services.Player;
using GameServer.Tables;
using Google.Protobuf;
using MongoDB.Driver;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.GameLogic.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameChangeJobReq)]
public class ChangeJobHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;
    private readonly LubanTableLoader _tables;

    public ChangeJobHandler(PlayerSessionManager session, INetworkSender network, LubanTableLoader tables)
    {
        _session = session;
        _network = network;
        _tables = tables;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return MakeError(PCommon.ErrorCode.Unauthorized);

        var req = PGame.ChangeJobRequest.Parser.ParseFrom(data);
        var targetJob = req.TargetJob;

        // 验证目标职业（从 Luban 职业表查询）
        var jobRow = _tables.GetJobByName(targetJob);
        if (jobRow == null)
            return MakeError(PCommon.ErrorCode.InvalidRequest, $"unknown job: {targetJob}");

        // 不能转成当前职业
        if (player.Job == targetJob)
            return MakeError(PCommon.ErrorCode.InvalidRequest, "already this job");

        // 1. 保存当前职业技能到 JobSkills[当前Job]
        var currentJob = player.Job;
        if (!string.IsNullOrEmpty(currentJob))
        {
            player.JobSkills[currentJob] = new JobSkillData
            {
                LearnedSkills = new List<int>(player.LearnedSkills),
                EquippedSkills = new List<int>(player.EquippedSkills),
            };
        }

        // 2. 切换 Job
        player.Job = targetJob;

        // 3. 从 JobSkills[targetJob] 恢复技能，或从职业表取默认技能
        if (player.JobSkills.TryGetValue(targetJob, out var saved))
        {
            player.LearnedSkills = new List<int>(saved.LearnedSkills);
            player.EquippedSkills = new List<int>(saved.EquippedSkills);
            // 迁移：旧存档可能包含普通攻击(id=1)，移除
            player.LearnedSkills.Remove(1);
            player.EquippedSkills.Remove(1);
        }
        else
        {
            var (learned, equipped) = _tables.GetJobDefaultSkills(targetJob);
            player.LearnedSkills = learned;
            player.EquippedSkills = equipped;
        }

        // 4. 持久化到 MongoDB
        await _session.Roles.Update(player.RoleId, u => u
            .Set(r => r.Job, player.Job)
            .Set(r => r.LearnedSkills, player.LearnedSkills)
            .Set(r => r.EquippedSkills, player.EquippedSkills)
            .Set(r => r.JobSkills, player.JobSkills));

        // 5. 更新内存 MapPlayerState
        var mapPlayer = _session.MapService.GetPlayerOnMap(player.CurrentMap, claims.AccountId);
        if (mapPlayer != null)
        {
            mapPlayer.Job = player.Job;
            mapPlayer.EquippedSkills = new List<int>(player.EquippedSkills);
        }

        // 6. 推送 FullRoleInfo 给客户端
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var roleInfo = PlayerProtoMapper.BuildRoleInfo(player, now);
        _network.SendToAccount(claims.AccountId, claims.ServerId, (int)PProtocol.MessageId.GameRoleAttrNotify, roleInfo.ToByteArray());

        // 7. 返回 ChangeJobResponse
        var rsp = new PGame.ChangeJobResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "",
            CurrentJob = player.Job,
        };
        foreach (var sid in player.LearnedSkills) rsp.LearnedSkills.Add((uint)sid);
        foreach (var sid in player.EquippedSkills) rsp.EquippedSkills.Add((uint)sid);
        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code, string msg = "")
        => new PGame.ChangeJobResponse { Code = code, Message = msg }.ToByteArray();
}
