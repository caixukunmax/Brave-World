using GameServer.Common.Config;
using GameServer.Common.Net;
using GameServer.Common.Security;
using GameServer.Database.Models;
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

[HandlesMessage((int)PProtocol.MessageId.GameEnterGameReq)]
public class EnterGameHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;
    private readonly IMonsterAiService _monsterAi;
    private readonly IDropService _dropService;
    private readonly LubanTableLoader _tables;
    private readonly MapDataProvider _mapData;

    public EnterGameHandler(PlayerSessionManager session, INetworkSender network, IMonsterAiService monsterAi, IDropService dropService, LubanTableLoader tables, MapDataProvider mapData)
    {
        _session = session;
        _network = network;
        _monsterAi = monsterAi;
        _dropService = dropService;
        _tables = tables;
        _mapData = mapData;
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

        // 如果角色记录的地图已不存在（例如地图被删除），迁移到注册表第一张可用地图
        if (_mapData.GetRegistryEntry(mapName) == null)
        {
            var fallback = _mapData.GetAllRegistryEntries().Values.FirstOrDefault();
            if (fallback == null)
            {
                _session.Logger.LogError("EnterGame: roleId={RoleId} currentMap={CurrentMap} 不存在且注册表为空", roleId, mapName);
                return MakeError(PCommon.ErrorCode.UnknownError);
            }

            var (spawnX, spawnY) = (fallback.spawn_x, fallback.spawn_y);
            _session.Logger.LogWarning("EnterGame: roleId={RoleId} 地图 {OldMap} 已不存在，迁移到 {NewMap} ({SpawnX},{SpawnY})",
                roleId, mapName, fallback.map_name, spawnX, spawnY);

            role.CurrentMap = fallback.map_name;
            role.GridX = spawnX;
            role.GridY = spawnY;
            mapName = fallback.map_name;

            await _session.Roles.Update(roleId, u => u
                .Set(r => r.CurrentMap, role.CurrentMap)
                .Set(r => r.GridX, role.GridX)
                .Set(r => r.GridY, role.GridY));
        }

        // 按角色 footprint 校验出生点，若当前坐标无法容纳则修正到最近的合法锚点
        int sizeX = role.GridSizeX > 0 ? role.GridSizeX : 1;
        int sizeY = role.GridSizeY > 0 ? role.GridSizeY : 1;
        var corrected = _session.MapService.FindNearestWalkableForFootprint(mapName, role.GridX, role.GridY, sizeX, sizeY);
        if (corrected != null && (corrected.Value.x != role.GridX || corrected.Value.y != role.GridY))
        {
            _session.Logger.LogWarning("EnterGame: roleId={RoleId} spawn corrected from ({OldX},{OldY}) to ({NewX},{NewY}) footprint={SizeX}x{SizeY}",
                roleId, role.GridX, role.GridY, corrected.Value.x, corrected.Value.y, sizeX, sizeY);
            role.GridX = corrected.Value.x;
            role.GridY = corrected.Value.y;
            await _session.Roles.Update(roleId, u => u
                .Set(r => r.GridX, role.GridX)
                .Set(r => r.GridY, role.GridY));
        }

        _session.MapService.PlayerEnter(new PlayerSnapshot
        {
            AccountId = claims.AccountId,
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            ServerId = claims.ServerId,
            GridX = role.GridX,
            GridY = role.GridY,
            SizeX = sizeX,
            SizeY = sizeY,
            Level = role.Level,
            CurrentMap = mapName,
            Hp = hp, MaxHp = hp, Mp = mp, MaxMp = mp,
            Patk = patk, Matk = matk, Pdef = pdef, Mdef = mdef,
            MpRegen = mpRegen,
            Job = role.Job,
            MoveSpeedMs = role.MoveSpeedMs > 0 ? role.MoveSpeedMs : GameConstants.BaseMoveSpeedMs,
            PreferredSkillId = role.PreferredSkillId,
            EquippedSkills = new List<int>(role.EquippedSkills),
        });

        var rsp = new PGame.EnterGameResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "",
            ServerTime = (uint)now,
        };
        rsp.RoleInfo = PlayerProtoMapper.BuildRoleInfo(role, now);

        var items = await PlayerProtoMapper.BuildItemsProto(_session.Inventory, role, _tables);
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
                SizeX = m.SizeX > 0 ? m.SizeX : 1,
                SizeY = m.SizeY > 0 ? m.SizeY : 1,
                Direction = m.Direction,
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
                    SizeX = n.SizeX > 0 ? n.SizeX : 1,
                    SizeY = n.SizeY > 0 ? n.SizeY : 1,
                    Direction = n.Direction,
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
                    int decoration = _session.MapService.GetDecorationType(mapName, x, y);
                    if (terrain != 0 || decoration != 0)
                    {
                        notify.Tiles.Add(new PGame.TileInfo
                        {
                            X = x,
                            Y = y,
                            TerrainType = terrain,
                            DecorationType = decoration,
                            // 阶段 1：服务端地图数据暂不支持多格建筑，默认 1；客户端从 EntityProfile 读取真实占地
                            SizeX = 1,
                            SizeY = 1,
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
