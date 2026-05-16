using GameServer.Common.Net;
using GameServer.Database.Models;
using GameServer.Services.Core;
using GameServer.Services.Map.Combat;
using GameServer.Tables;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PCommon = global::Common;
using PGame = global::Game;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)global::Protocol.MessageId.GameUseItemReq)]
public class UseItemHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly LubanTableLoader _tables;
    private readonly INetworkSender _network;
    private readonly ILogger<UseItemHandler> _logger;

    public UseItemHandler(PlayerSessionManager session, LubanTableLoader tables, INetworkSender network, ILogger<UseItemHandler> logger)
    {
        _session = session;
        _tables = tables;
        _network = network;
        _logger = logger;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return MakeError(PCommon.ErrorCode.Unauthorized);

        var req = PGame.UseItemRequest.Parser.ParseFrom(data);
        var itemId = (int)req.ItemId;
        var count = (int)req.Count;
        if (itemId == 0 || count == 0)
            return MakeError(PCommon.ErrorCode.InvalidRequest);

        var itemCfg = _tables.GetItem(itemId);
        if (itemCfg == null)
            return MakeError(PCommon.ErrorCode.NotFound, "item config not found");

        // 扣除物品
        var ok = await _session.Inventory.RemoveItem(player.RoleId, itemId, count);
        if (!ok)
            return new PGame.UseItemResponse { Code = PCommon.ErrorCode.NotFound, Message = "item not enough" }.ToByteArray();

        // 执行物品效果
        ApplyItemEffect(player, itemCfg, count);

        // 持久化角色属性（HP/MP/Gold/Exp 可能已变）
        await _session.Roles.Update(player.RoleId, u => u.Combine(
            u.Set(r => r.Hp, player.Hp),
            u.Set(r => r.Mp, player.Mp),
            u.Set(r => r.Gold, player.Gold),
            u.Set(r => r.Exp, player.Exp),
            u.Set(r => r.Level, player.Level),
            u.Set(r => r.MaxHp, player.MaxHp),
            u.Set(r => r.MaxMp, player.MaxMp),
            u.Set(r => r.Patk, player.Patk),
            u.Set(r => r.Matk, player.Matk),
            u.Set(r => r.Pdef, player.Pdef),
            u.Set(r => r.Mdef, player.Mdef)));

        // 推送最新角色属性
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var roleInfo = PlayerProtoMapper.BuildRoleInfo(player, now);
        _network.SendToAccount(claims.AccountId, claims.ServerId,
            (int)global::Protocol.MessageId.GameRoleAttrNotify, roleInfo.ToByteArray());

        var rsp = new PGame.UseItemResponse { Code = PCommon.ErrorCode.Success, Message = "" };
        var items = await PlayerProtoMapper.BuildItemsProto(_session.Inventory, player.RoleId);
        foreach (var item in items) rsp.Items.Add(item);
        return rsp.ToByteArray();
    }

    private void ApplyItemEffect(Role player, ItemRow itemCfg, int count)
    {
        var effectType = itemCfg.EffectType?.ToLowerInvariant() ?? "";
        var value = itemCfg.EffectValue * count;

        if (string.IsNullOrEmpty(effectType) || value == 0)
        {
            _logger.LogInformation("[UseItem] no effect: item={ItemId} name={Name}",
                itemCfg.Id, itemCfg.Name);
            return;
        }

        switch (effectType)
        {
            case "heal":
                int oldHp = player.Hp;
                player.Hp = Math.Min(player.Hp + value, player.MaxHp);
                _logger.LogInformation("[UseItem] heal: player={AccountId} +{Value}hp {OldHp}→{NewHp}/{MaxHp}",
                    player.AccountId, value, oldHp, player.Hp, player.MaxHp);
                break;

            case "restore_mp":
                int oldMp = player.Mp;
                player.Mp = Math.Min(player.Mp + value, player.MaxMp);
                _logger.LogInformation("[UseItem] restore_mp: player={AccountId} +{Value}mp {OldMp}→{NewMp}/{MaxMp}",
                    player.AccountId, value, oldMp, player.Mp, player.MaxMp);
                break;

            case "add_gold":
                long oldGold = player.Gold;
                player.Gold += value;
                _logger.LogInformation("[UseItem] add_gold: player={AccountId} +{Value}gold {OldGold}→{NewGold}",
                    player.AccountId, value, oldGold, player.Gold);
                break;

            case "add_exp":
                int oldExp = player.Exp;
                player.Exp += value;
                _logger.LogInformation("[UseItem] add_exp: player={AccountId} +{Value}exp {OldExp}→{NewExp}",
                    player.AccountId, value, oldExp, player.Exp);
                CheckLevelUp(player);
                break;

            default:
                _logger.LogWarning("[UseItem] unknown effect_type={EffectType} item={ItemId}",
                    itemCfg.EffectType, itemCfg.Id);
                break;
        }
    }

    /// <summary>
    /// 检查升级（内联自 LevelUpService，避免引入跨模块依赖）
    /// </summary>
    private void CheckLevelUp(Role player)
    {
        int oldLevel = player.Level;
        int maxLevel = _tables.GetMaxLevel();

        while (player.Level < maxLevel)
        {
            var nextLevelCfg = _tables.GetLevelUp(player.Level + 1);
            if (nextLevelCfg == null) break;
            if (player.Exp < nextLevelCfg.ExpRequired) break;

            player.Level++;
            player.MaxHp += nextLevelCfg.Hp;
            player.MaxMp += nextLevelCfg.Mp;
            player.Patk += nextLevelCfg.Patk;
            player.Matk += nextLevelCfg.Matk;
            player.Pdef += nextLevelCfg.Pdef;
            player.Mdef += nextLevelCfg.Mdef;
            player.Hp = player.MaxHp;
            player.Mp = player.MaxMp;

            _logger.LogInformation("[UseItem] level up! player={AccountId} {OldLevel}→{NewLevel}",
                player.AccountId, oldLevel, player.Level);
        }

        // 推送升级通知（如果升级了）
        if (player.Level > oldLevel)
        {
            var mapPlayer = _session.MapService.GetPlayerOnMap(player.CurrentMap, player.AccountId);
            if (mapPlayer != null)
            {
                mapPlayer.Level = player.Level;
                mapPlayer.Hp = player.Hp;
                mapPlayer.MaxHp = player.MaxHp;
                mapPlayer.Mp = player.Mp;
                mapPlayer.MaxMp = player.MaxMp;
            }

            var notify = new PGame.LevelUpNotify
            {
                OldLevel = oldLevel,
                NewLevel = player.Level,
                MaxHp = player.MaxHp,
                MaxMp = player.MaxMp,
                Hp = player.Hp,
                Mp = player.Mp,
                Patk = player.Patk,
                Matk = player.Matk,
                Pdef = player.Pdef,
                Mdef = player.Mdef,
            };
            _network.SendToAccount(player.AccountId, player.ServerId,
                (int)global::Protocol.MessageId.GameLevelUpNotify, notify.ToByteArray());
        }
    }

    private static byte[] MakeError(PCommon.ErrorCode code, string message = "")
        => new PCommon.Response { Code = code, Message = message }.ToByteArray();
}
