using GameServer.Common.Config;
using GameServer.Common.Net;
using GameServer.Common.Security;
using GameServer.Database.Models;
using GameServer.Services.Core;
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

    public CreateRoleHandler(PlayerSessionManager session, MapDataProvider mapData)
    {
        _session = session;
        _mapData = mapData;
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
        };
        await _session.Roles.Create(roleData);

        var initItems = PlayerProtoMapper.ParseInitItems(GameConstants.InitItems);
        foreach (var item in initItems)
            await _session.Inventory.AddItem(roleId, item.ItemId, item.Count);

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
        _session.MapService.BroadcastToMap(mapName, (int)PProtocol.MessageId.GameMapInfoSyncNotify, notify.ToByteArray());
    }

    private static byte[] MakeError(PCommon.ErrorCode code)
        => new PCommon.Response { Code = code, Message = "" }.ToByteArray();
}
