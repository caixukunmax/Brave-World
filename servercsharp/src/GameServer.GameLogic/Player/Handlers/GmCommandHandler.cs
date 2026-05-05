using GameServer.Common;
using GameServer.Common.Buffs;
using GameServer.Common.Models;
using GameServer.Common.Net;
using GameServer.Database.Models;
using GameServer.Services.Map.Combat;
using GameServer.Services.Core;
using GameServer.Tables;
using MongoDB.Driver;
using Google.Protobuf;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)PProtocol.MessageId.GameGmReq)]
public class GmCommandHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;
    private readonly LubanTableLoader _tables;

    public GmCommandHandler(PlayerSessionManager session, INetworkSender network, LubanTableLoader tables)
    {
        _session = session;
        _network = network;
        _tables = tables;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Unauthorized, Message = "player not online" }.ToByteArray();

        var req = PGame.GmCommandRequest.Parser.ParseFrom(data);
        var parts = req.Command.Split(',');
        var cmd = parts[0];

        if (cmd == "additem")
        {
            var itemId = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            var count = parts.Length > 2 ? Math.Max(1, int.Parse(parts[2])) : 1;
            if (itemId == 0)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = "usage: additem,itemId,count" }.ToByteArray();

            await _session.Inventory.AddItem(player.RoleId, itemId, count);
            var rsp = new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Success, Message = $"added {count}x {itemId}" };
            var items = await PlayerProtoMapper.BuildItemsProto(_session.Inventory, player.RoleId);
            foreach (var item in items) rsp.Items.Add(item);
            return rsp.ToByteArray();
        }

        if (cmd == "teleport")
        {
            var tx = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            var ty = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            var mapName = player.CurrentMap;
            var walkable = _session.MapService.FindNearestWalkable(mapName, tx, ty);
            if (walkable == null)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Forbidden, Message = $"no walkable cell near ({tx},{ty})" }.ToByteArray();

            player.GridX = walkable.Value.x;
            player.GridY = walkable.Value.y;
            _session.MapService.PlayerMove(claims.AccountId, mapName, walkable.Value.x, walkable.Value.y);
            return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Success, Message = $"TELEPORT:{walkable.Value.x}:{walkable.Value.y}" }.ToByteArray();
        }

        if (cmd == "addchest")
        {
            var chestCfgId = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            var cx = parts.Length > 2 ? int.Parse(parts[2]) : -1;
            var cy = parts.Length > 3 ? int.Parse(parts[3]) : -1;
            if (chestCfgId <= 0 || cx < 0 || cy < 0)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = "usage: addchest,configId,x,y" }.ToByteArray();

            var mapName = player.CurrentMap;
            var mapId = GameConstants.DefaultMapId;
            var seq = await _session.Chests.AllocateEntitySeq(mapId, (int)MapEntityType.Chest, new HashSet<int>());
            var newId = InstanceId.Make(mapId, MapEntityType.Chest, seq);
            await _session.Chests.AddGmChest(newId, mapId, (int)MapEntityType.Chest, chestCfgId, cx, cy, "");

            var notify = new PGame.ChestUpdateNotify();
            notify.Chests.Add(new PGame.ChestInfo { ChestId = (uint)newId, X = cx, Y = cy });
            _session.MapService.BroadcastToMap(mapName, (int)PProtocol.MessageId.GameChestUpdateNotify, notify.ToByteArray());

            return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Success, Message = $"ADDCH:{newId}:{cx}:{cy}" }.ToByteArray();
        }

        if (cmd == "setattr")
        {
            if (parts.Length < 3)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = "usage: setattr,attrName,value" }.ToByteArray();

            var attrName = parts[1];
            if (!int.TryParse(parts[2], out var attrValue))
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = "value must be integer" }.ToByteArray();

            var attrKey = RoleAttrs.NameToKey(attrName);
            if (attrKey == null)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = $"unknown attr: {attrName}. valid: hp,max_hp,mp,max_mp,agility,patk,matk,pdef,mdef,mp_regen,move_speed" }.ToByteArray();

            // 更新 Role 内存对象
            ApplyAttrToRole(player, attrName, attrValue);

            // 更新 MapPlayerState（战斗系统实时生效）
            var mapPlayer = _session.MapService.GetPlayerOnMap(player.CurrentMap, claims.AccountId);
            if (mapPlayer != null)
                ApplyAttrToMapPlayer(mapPlayer, attrName, attrValue);

            // 持久化到 MongoDB
            await _session.Roles.Update(player.RoleId, u => BuildAttrUpdate(u, attrName, attrValue));

            // 广播更新后的 FullRoleInfo 给该玩家
            var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var roleInfo = PlayerProtoMapper.BuildRoleInfo(player, now);
            _network.SendToAccount(claims.AccountId, claims.ServerId, (int)PProtocol.MessageId.GameRoleAttrNotify, roleInfo.ToByteArray());

            return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Success, Message = $"setattr,{attrName},{attrValue},ok" }.ToByteArray();
        }

        if (cmd == "addexp")
        {
            var expValue = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            if (expValue <= 0)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = "usage: addexp,value (must be > 0)" }.ToByteArray();

            var svc = new LevelUpService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<LevelUpService>.Instance,
                _session, _tables, _network, _session.MapService);

            bool leveledUp = svc.AddExp(claims.AccountId, expValue);
            string msg = $"added {expValue} exp, level={player.Level}, exp={player.Exp}" + (leveledUp ? " LEVELED UP!" : "");
            return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Success, Message = msg }.ToByteArray();
        }

        if (cmd == "learnskill")
        {
            var skillId = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            if (skillId <= 1)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = "usage: learnskill,skillId (must be > 1)" }.ToByteArray();

            var cfg = SkillPipeline.GetSkillConfigStatic(skillId);
            if (cfg == null)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = $"skill {skillId} not found" }.ToByteArray();

            if (player.LearnedSkills.Contains(skillId))
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = $"skill {skillId} ({cfg.Name}) already learned" }.ToByteArray();

            player.LearnedSkills.Add(skillId);
            await _session.Roles.Update(player.RoleId, u => u.AddToSet(r => r.LearnedSkills, skillId));

            var rsp = new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Success, Message = $"learned skill {skillId} ({cfg.Name})" };
            foreach (var sid in player.LearnedSkills) rsp.LearnedSkills.Add((uint)sid);
            foreach (var sid in player.EquippedSkills) rsp.EquippedSkills.Add((uint)sid);
            return rsp.ToByteArray();
        }

        if (cmd == "addbuff")
        {
            var buffId = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            if (buffId <= 0)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = "usage: addbuff,buffId" }.ToByteArray();

            var mapPlayer = _session.MapService.GetPlayerOnMap(player.CurrentMap, claims.AccountId);
            if (mapPlayer == null)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = $"cannot add buff: player not on map" }.ToByteArray();

            var now = Environment.TickCount64;
            var cfg = _tables.GetBuff(buffId);
            long expireTime = cfg != null && cfg.Duration > 0 ? now + (long)(cfg.Duration * 1000) : 0;
            int shield = cfg?.ShieldBase ?? 0;

            var buff = new BuffInstance
            {
                BuffId = buffId,
                Stacks = 1,
                ApplyTime = now,
                ExpireTime = expireTime,
                CasterId = claims.AccountId,
                ShieldRemaining = shield,
            };
            mapPlayer.Buffs.AddBuff(buff);

            var buffList = BuildBuffList(mapPlayer.Buffs);
            var addNotify = new PGame.BuffUpdateNotify { EntityId = (ulong)claims.AccountId };
            foreach (var b in buffList)
                addNotify.Buffs.Add(new PGame.BuffUpdateNotify.Types.BuffEntry
                {
                    BuffId = b.BuffId, BuffName = b.BuffName, Stacks = b.Stacks,
                    RemainingTime = b.RemainingTime, ShieldAmount = b.ShieldAmount,
                });
            _network.SendToAccount(claims.AccountId, claims.ServerId, (int)PProtocol.MessageId.GameBuffUpdateNotify, addNotify.ToByteArray());
            return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Success, Message = $"added buff {buffId}" }.ToByteArray();
        }

        if (cmd == "removebuff")
        {
            var buffId = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            if (buffId <= 0)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = "usage: removebuff,buffId" }.ToByteArray();

            var mapPlayer = _session.MapService.GetPlayerOnMap(player.CurrentMap, claims.AccountId);
            if (mapPlayer == null)
                return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = $"cannot remove buff: player not on map" }.ToByteArray();

            mapPlayer.Buffs.RemoveBuff(buffId);

            var buffList = BuildBuffList(mapPlayer.Buffs);
            var rmNotify = new PGame.BuffUpdateNotify { EntityId = (ulong)claims.AccountId };
            foreach (var b in buffList)
                rmNotify.Buffs.Add(new PGame.BuffUpdateNotify.Types.BuffEntry
                {
                    BuffId = b.BuffId, BuffName = b.BuffName, Stacks = b.Stacks,
                    RemainingTime = b.RemainingTime, ShieldAmount = b.ShieldAmount,
                });
            _network.SendToAccount(claims.AccountId, claims.ServerId, (int)PProtocol.MessageId.GameBuffUpdateNotify, rmNotify.ToByteArray());
            return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.Success, Message = $"removed buff {buffId}" }.ToByteArray();
        }

        return new PGame.GmCommandResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = $"unknown command: {cmd}" }.ToByteArray();
    }

    private static void ApplyAttrToRole(Role role, string attrName, int value)
    {
        switch (attrName)
        {
            case "hp": role.Hp = value; break;
            case "max_hp": role.MaxHp = value; break;
            case "mp": role.Mp = value; break;
            case "max_mp": role.MaxMp = value; break;
            case "agility": role.Agility = value; break;
            case "patk": role.Patk = value; break;
            case "matk": role.Matk = value; break;
            case "pdef": role.Pdef = value; break;
            case "mdef": role.Mdef = value; break;
            case "mp_regen": role.MpRegen = value; break;
            case "move_speed": role.MoveSpeedMs = value; break;
        }
    }

    private static UpdateDefinition<Role> BuildAttrUpdate(UpdateDefinitionBuilder<Role> u, string attrName, int value)
    {
        return attrName switch
        {
            "hp" => u.Set(r => r.Hp, value),
            "max_hp" => u.Set(r => r.MaxHp, value),
            "mp" => u.Set(r => r.Mp, value),
            "max_mp" => u.Set(r => r.MaxMp, value),
            "agility" => u.Set(r => r.Agility, value),
            "patk" => u.Set(r => r.Patk, value),
            "matk" => u.Set(r => r.Matk, value),
            "pdef" => u.Set(r => r.Pdef, value),
            "mdef" => u.Set(r => r.Mdef, value),
            "mp_regen" => u.Set(r => r.MpRegen, value),
            "move_speed" => u.Set(r => r.MoveSpeedMs, value),
            _ => u.Set(r => r.Hp, value), // fallback
        };
    }

    private static void ApplyAttrToMapPlayer(MapPlayerState player, string attrName, int value)
    {
        switch (attrName)
        {
            case "hp": player.Hp = value; break;
            case "max_hp": player.MaxHp = value; break;
            case "mp": player.Mp = value; break;
            case "max_mp": player.MaxMp = value; break;
            case "agility": player.Agility = value; break;
            case "patk": player.Patk = value; break;
            case "matk": player.Matk = value; break;
            case "pdef": player.Pdef = value; break;
            case "mdef": player.Mdef = value; break;
            case "mp_regen": player.MpRegen = value; break;
            case "move_speed": player.MoveSpeedMs = value; break;
        }
    }

    private List<(int BuffId, string BuffName, int Stacks, float RemainingTime, int ShieldAmount)> BuildBuffList(BuffContainer buffs)
    {
        var result = new List<(int, string, int, float, int)>();
        var now = Environment.TickCount64;
        foreach (var b in buffs.Buffs)
        {
            var cfg = _tables.GetBuff(b.BuffId);
            string name = cfg?.Name ?? $"Buff{b.BuffId}";
            float remaining = b.ExpireTime <= 0 ? -1f : (float)(b.ExpireTime - now) / 1000f;
            if (remaining < 0 && b.ExpireTime > 0) remaining = 0;
            result.Add((b.BuffId, name, b.Stacks, remaining, b.ShieldRemaining));
        }
        return result;
    }
}
