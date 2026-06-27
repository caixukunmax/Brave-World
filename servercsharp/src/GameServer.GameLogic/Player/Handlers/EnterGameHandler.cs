using GameServer.Common.Net;
using GameServer.Common.Security;
using GameServer.Database.Models;
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
    private readonly IDropService _dropService;
    private readonly LubanTableLoader _tables;

    public EnterGameHandler(PlayerSessionManager session, INetworkSender network, IMonsterAiService monsterAi, IDropService dropService, LubanTableLoader tables)
    {
        _session = session;
        _network = network;
        _monsterAi = monsterAi;
        _dropService = dropService;
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

        // 按当前等级重新计算完整属性（修复老玩家/等级成长）
        var (hp, mp, patk, matk, pdef, mdef, mpRegen) = _tables.GetPlayerAttrsByLevel(role.Level);
        role.MaxHp = hp;
        role.MaxMp = mp;
        role.Patk = patk; role.Matk = matk;
        role.Pdef = pdef; role.Mdef = mdef;
        role.MpRegen = mpRegen;
        // 如果当前血量/蓝量超过上限则截断，否则保留（支持残血下线）
        if (role.Hp > role.MaxHp) role.Hp = role.MaxHp;
        if (role.Mp > role.MaxMp) role.Mp = role.MaxMp;
        if (role.Hp <= 0) role.Hp = 1; // 至少留1点血，避免登录即死

        // 补初始化 JobSkills（老角色可能没有这个字段）
        if (role.JobSkills.Count == 0 && !string.IsNullOrEmpty(role.Job))
        {
            role.JobSkills[role.Job] = new JobSkillData
            {
                LearnedSkills = new List<int>(role.LearnedSkills),
                EquippedSkills = new List<int>(role.EquippedSkills),
            };
            await _session.Roles.Update(roleId, u => u.Set(r => r.JobSkills, role.JobSkills));
        }

        // 迁移：从 EquippedSkills 中移除普通攻击(id=1)，普通攻击不可装备
        if (role.EquippedSkills.Remove(1))
        {
            await _session.Roles.Update(roleId, u => u.Set(r => r.EquippedSkills, role.EquippedSkills));
        }
        // 确保 LearnedSkills 包含普通攻击
        if (!role.LearnedSkills.Contains(1))
        {
            role.LearnedSkills.Insert(0, 1);
            await _session.Roles.Update(roleId, u => u.Set(r => r.LearnedSkills, role.LearnedSkills));
        }

        var mapName = MapNameNormalizer.Normalize(role.CurrentMap);
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
            Patk = patk, Matk = matk, Pdef = pdef, Mdef = mdef,
            MpRegen = mpRegen,
            Job = role.Job,
            MoveSpeedMs = role.MoveSpeedMs > 0 ? role.MoveSpeedMs : GameConstants.BaseMoveSpeedMs,
            EquippedSkills = role.EquippedSkills,
        });

        var rsp = new PGame.EnterGameResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "",
            ServerTime = (uint)now,
        };
        rsp.RoleInfo = PlayerProtoMapper.BuildRoleInfo(role, now);

        var items = await PlayerProtoMapper.BuildItemsProto(_session.Inventory, roleId, _tables);
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

        // 推送 NPC 列表
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

        // 推送掉落物列表
        var drops = _dropService.GetDrops(mapName);
        notify.Drops.AddRange(drops);

        // 推送地形数据（只同步非普通地形，减少数据量）
        var terrainData = _session.MapService.GetMapTerrainData(mapName);
        if (terrainData != null)
        {
            var (width, height, terrainTypes) = terrainData.Value;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int terrain = terrainTypes[x, y];
                    if (terrain != 0)
                    {
                        notify.Tiles.Add(new PGame.TileInfo
                        {
                            X = x,
                            Y = y,
                            TerrainType = terrain,
                        });
                    }
                }
            }
        }

        _network.SendToAccount(claims.AccountId, claims.ServerId,
            (int)PProtocol.MessageId.GameMapInfoSyncNotify, notify.ToByteArray());

        _session.Logger.LogInformation("EnterGame: roleId={RoleId} map={Map} tiles={Tiles}",
            roleId, mapName, notify.Tiles.Count);

        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code)
        => new PCommon.Response { Code = code, Message = "" }.ToByteArray();
}
