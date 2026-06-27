using GameServer.Common.Config;
using GameServer.Common.Net;
using GameServer.Common.Security;
using GameServer.Database.Models;
using GameServer.GameLogic.Inventory;
using GameServer.Services.Core;
using GameServer.Tables;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameCreateRoleReq)]
public class CreateRoleHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly MapDataProvider _mapData;
    private readonly LubanTableLoader _tables;

    public CreateRoleHandler(PlayerSessionManager session, MapDataProvider mapData, LubanTableLoader tables)
    {
        _session = session;
        _mapData = mapData;
        _tables = tables;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.CreateRoleRequest.Parser.ParseFrom(data);
        var roleName = req.RoleName;

        if (string.IsNullOrEmpty(roleName) || roleName.Length < 2)
            return MakeError(PCommon.ErrorCode.RoleNameTooShort);
        if (roleName.Length > 12)
            return MakeError(PCommon.ErrorCode.RoleNameTooLong);

        if (await _session.Roles.CheckNameExists(claims.ServerId, roleName))
            return MakeError(PCommon.ErrorCode.RoleNameExists);

        var roleCount = await _session.Roles.CountByAccountAndServer(claims.AccountId, claims.ServerId);
        if (roleCount >= 3)
            return MakeError(PCommon.ErrorCode.RoleCountLimit);

        var roleId = await _session.Roles.GetNextRoleId();
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 从地图注册表获取出生点（取第一个地图）
        var allEntries = _mapData.GetAllRegistryEntries();
        var firstMap = allEntries.Values.FirstOrDefault();
        var birthMap = firstMap?.map_name ?? GameConstants.DefaultMapName;
        var (spawnX, spawnY) = firstMap != null
            ? (firstMap.spawn_x, firstMap.spawn_y)
            : (GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnY);

        // 按 Lv1 初始化属性
        var (baseHp, baseMp, basePatk, baseMatk, basePdef, baseMdef, baseMpRegen) = _tables.GetPlayerAttrsByLevel(1);

        var roleData = new Role
        {
            RoleId = roleId,
            AccountId = claims.AccountId,
            ServerId = claims.ServerId,
            RoleName = roleName,
            Gold = GameConstants.StartGold,
            Diamond = GameConstants.StartDiamond,
            TotalPower = GameConstants.StartTotalPower,
            CreateTime = now,
            LastLoginTime = now,
            Job = "战士",
            Title = "新手",
            Status = "在线",
            CurrentMap = birthMap,
            GridX = spawnX,
            GridY = spawnY,
            MoveSpeedMs = GameConstants.BaseMoveSpeedMs,
            Hp = baseHp, MaxHp = baseHp,
            Mp = baseMp, MaxMp = baseMp,
            Patk = basePatk, Matk = baseMatk,
            Pdef = basePdef, Mdef = baseMdef,
            MpRegen = baseMpRegen,
            LearnedSkills = _tables.GetJobDefaultSkills("战士").learned,
            EquippedSkills = _tables.GetJobDefaultSkills("战士").equipped,
        };

        // 初始化 JobSkills — 战士默认技能
        var warriorSkills = _tables.GetJobDefaultSkills("战士");
        roleData.JobSkills["战士"] = new JobSkillData
        {
            LearnedSkills = warriorSkills.learned,
            EquippedSkills = warriorSkills.equipped,
        };
        await _session.Roles.Create(roleData);

        var initItems = PlayerProtoMapper.ParseInitItems(GameConstants.InitItems);
        foreach (var item in initItems)
            await _session.Inventory.AddItem(roleId, item.ItemId, item.Count);

        foreach (var item in initItems)
            InventoryOrderHelper.AppendItem(roleData.InventoryOrder, item.ItemId);
        await _session.Roles.UpdateInventoryOrder(roleData.RoleId, roleData.InventoryOrder);

        var rsp = new PGame.CreateRoleResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "",
            ServerTime = (uint)now,
        };
        rsp.RoleInfo = PlayerProtoMapper.BuildRoleInfo(roleData, now);
        foreach (var item in initItems)
            rsp.Items.Add(new PGame.ItemInfo { ItemId = (uint)item.ItemId, Count = (uint)item.Count });
        rsp.Chests.Add(await PlayerProtoMapper.BuildChestsProto(roleId, 1));

        _session.Logger.LogInformation("CreateRole: {Name} roleId={RoleId}", roleName, roleId);

        SendMapInfoSync(claims.AccountId, claims.ServerId, birthMap, GameConstants.DefaultMapId, roleId);

        return rsp.ToByteArray();
    }

    private void SendMapInfoSync(long accountId, int serverId, string mapName, int mapId, long roleId)
    {
        var notify = new PGame.MapInfoSyncNotify { MapName = mapName };

        // 添加 NPC 列表
        var npcMgr = _session.NpcManager;
        if (npcMgr != null)
        {
            var npcs = npcMgr.GetNpcsOnMap(mapName);
            foreach (var n in npcs)
            {
                notify.Npcs.Add(new PGame.NpcInfo
                {
                    NpcInstanceId = (ulong)n.InstanceId,
                    NpcName = n.Name,
                    NpcType = n.NpcType,
                    X = n.X,
                    Y = n.Y,
                });
            }
        }

        _session.MapService.BroadcastToMap(mapName, (int)PProtocol.MessageId.GameMapInfoSyncNotify, notify.ToByteArray());
    }

    private static byte[] MakeError(PCommon.ErrorCode code)
        => new PCommon.Response { Code = code, Message = "" }.ToByteArray();
}
