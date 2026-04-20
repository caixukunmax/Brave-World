using GameServer.Common.Net;
using GameServer.Common.Security;
using GameServer.Services.Core;
using GameServer.Services.Map;
using GameServer.Tables;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameEnterGameReq)]
public class EnterGameHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;
    private readonly IMonsterAiService _monsterAi;
    private readonly LubanTableLoader _tables;

    public EnterGameHandler(PlayerSessionManager session, INetworkSender network, IMonsterAiService monsterAi, LubanTableLoader tables)
    {
        _session = session;
        _network = network;
        _monsterAi = monsterAi;
        _tables = tables;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        var req = PGame.EnterGameRequest.Parser.ParseFrom(data);
        var roleId = (long)req.RoleId;
        if (roleId == 0)
            return MakeError(PCommon.ErrorCode.InvalidRequest);

        var role = await _session.Roles.FindById(roleId);
        if (role == null)
            return MakeError(PCommon.ErrorCode.RoleNotFound);

        if (role.AccountId != claims.AccountId || role.ServerId != claims.ServerId)
            return MakeError(PCommon.ErrorCode.Forbidden);

        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _session.Roles.Update(roleId, u => u.Set(r => r.LastLoginTime, now));
        _session.SetOnline(claims.AccountId, role);

        var (hp, mp, agility, patk, matk, pdef, mdef) = _tables.GetPlayerBaseAttrs();
        role.Hp = hp; role.MaxHp = hp;
        role.Mp = mp; role.MaxMp = mp;
        role.Agility = agility;
        role.Patk = patk; role.Matk = matk;
        role.Pdef = pdef; role.Mdef = mdef;

        var mapName = role.CurrentMap;
        _session.MapService.PlayerEnter(new PlayerSnapshot
        {
            AccountId = claims.AccountId,
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            ServerId = claims.ServerId,
            GridX = role.GridX,
            GridY = role.GridY,
            Level = role.Level,
            CurrentMap = mapName,
            Hp = hp, MaxHp = hp, Mp = mp, MaxMp = mp,
            Agility = agility, Patk = patk, Matk = matk, Pdef = pdef, Mdef = mdef,
            Job = role.Job,
            MoveSpeedMs = role.MoveSpeedMs > 0 ? role.MoveSpeedMs : GameConstants.BaseMoveSpeedMs,
        });

        var rsp = new PGame.EnterGameResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "",
            ServerTime = (uint)now,
        };
        rsp.RoleInfo = PlayerProtoMapper.BuildRoleInfo(role, now);

        var items = await PlayerProtoMapper.BuildItemsProto(_session.Inventory, roleId);
        foreach (var item in items)
            rsp.Items.Add(item);

        rsp.Chests.Add(await PlayerProtoMapper.BuildChestsProto(roleId, GameConstants.DefaultMapId));

        _session.Logger.LogInformation("EnterGame: roleId={RoleId}", roleId);

        // 推送地图信息（怪物 + 宝箱）
        var notify = new PGame.MapInfoSyncNotify { MapName = mapName };

        var monsters = _monsterAi.GetMonstersOnMap(mapName);
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

        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code)
        => new PCommon.Response { Code = code, Message = "" }.ToByteArray();
}
