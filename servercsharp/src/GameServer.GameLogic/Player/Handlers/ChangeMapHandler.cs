using GameServer.Common.Config;
using GameServer.Common.Net;
using GameServer.Common.Security;
using GameServer.Services.Core;
using GameServer.Services.Map;
using GameServer.Tables;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameChangeMapReq)]
public class ChangeMapHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;
    private readonly MapDataProvider _mapData;
    private readonly IMonsterAiService _monsterAi;
    private readonly LubanTableLoader _tables;

    public ChangeMapHandler(PlayerSessionManager session, INetworkSender network, MapDataProvider mapData, IMonsterAiService monsterAi, LubanTableLoader tables)
    {
        _session = session;
        _network = network;
        _mapData = mapData;
        _monsterAi = monsterAi;
        _tables = tables;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.ChangeMapRequest.Parser.ParseFrom(data);
        var targetMap = req.TargetMap;

        if (string.IsNullOrEmpty(targetMap) || _mapData.GetMap(targetMap) == null)
            return MakeError(PCommon.ErrorCode.InvalidRequest, "target map not found");

        if (!_session.TryGetPlayer(claims.AccountId, out var role))
            return MakeError(PCommon.ErrorCode.Forbidden, "not online");

        var currentMap = role.CurrentMap;
        if (currentMap == targetMap)
            return MakeError(PCommon.ErrorCode.InvalidRequest, "already on this map");

        // 离开旧地图
        _session.MapService.PlayerLeave(claims.AccountId, currentMap);

        // 获取目标地图出生点
        var (spawnX, spawnY) = _mapData.GetSpawnPoint(targetMap);
        var walkable = _mapData.FindNearestWalkable(targetMap, spawnX, spawnY);
        if (walkable != null) { spawnX = walkable.Value.x; spawnY = walkable.Value.y; }

        var (hp, mp, agility, patk, matk, pdef, mdef, _) = _tables.GetPlayerBaseAttrs();

        // 更新角色数据
        role.CurrentMap = targetMap;
        role.GridX = spawnX;
        role.GridY = spawnY;

        // 进入新地图
        _session.MapService.PlayerEnter(new PlayerSnapshot
        {
            AccountId = claims.AccountId,
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            ServerId = claims.ServerId,
            GridX = spawnX,
            GridY = spawnY,
            Level = role.Level,
            CurrentMap = targetMap,
            Hp = hp, MaxHp = hp, Mp = mp, MaxMp = mp,
            Agility = agility, Patk = patk, Matk = matk, Pdef = pdef, Mdef = mdef,
            MpRegen = role.MpRegen,
        });

        // 推送新地图信息（怪物 + 宝箱）
        var notify = new PGame.MapInfoSyncNotify { MapName = targetMap };

        var monsters = _monsterAi.GetMonstersOnMap(targetMap);
        foreach (var m in monsters)
        {
            var info = new PGame.MonsterInfo
            {
                InstanceId = (uint)m.InstanceId,
                MonsterId = (uint)m.MonsterId,
                X = m.X,
                Y = m.Y,
                Name = m.Name,
                Level = (uint)m.Level,
            };
            info.Attrs.Add(new PGame.MonsterAttr { AttrKey = 1, AttrValue = m.Hp });
            info.Attrs.Add(new PGame.MonsterAttr { AttrKey = 2, AttrValue = m.Patk });
            info.Attrs.Add(new PGame.MonsterAttr { AttrKey = 3, AttrValue = m.Pdef });
            notify.Monsters.Add(info);
        }

        _network.SendToAccount(claims.AccountId, claims.ServerId,
            (int)PProtocol.MessageId.GameMapInfoSyncNotify, notify.ToByteArray());

        _session.Logger.LogInformation("ChangeMap: account={AccountId} {OldMap} -> {NewMap} spawn=({X},{Y})",
            claims.AccountId, currentMap, targetMap, spawnX, spawnY);

        // 持久化
        await _session.Roles.Update(role.RoleId, u => u
            .Set(r => r.CurrentMap, targetMap)
            .Set(r => r.GridX, spawnX)
            .Set(r => r.GridY, spawnY));

        return new PGame.ChangeMapResponse
        {
            Code = PCommon.ErrorCode.Success,
            MapName = targetMap,
            SpawnX = spawnX,
            SpawnY = spawnY,
        }.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code, string msg)
        => new PGame.ChangeMapResponse { Code = code, Message = msg }.ToByteArray();
}
