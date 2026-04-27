using GameServer.Database.Models;
using GameServer.Services.Core;
using GameServer.Services.Map;
using GameServer.Services.Player;
using GameServer.Tables;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 经验与升级服务 — 处理经验奖励、升级、属性成长
/// </summary>
public class LevelUpService
{
    private readonly ILogger<LevelUpService> _logger;
    private readonly PlayerSessionManager _session;
    private readonly LubanTableLoader _tables;
    private readonly INetworkSender _network;
    private readonly MapService _mapService;

    public LevelUpService(
        ILogger<LevelUpService> logger,
        PlayerSessionManager session,
        LubanTableLoader tables,
        INetworkSender network,
        MapService mapService)
    {
        _logger = logger;
        _session = session;
        _tables = tables;
        _network = network;
        _mapService = mapService;
    }

    /// <summary>
    /// 给玩家加经验，可能触发升级
    /// </summary>
    /// <returns>是否升级</returns>
    public bool AddExp(long accountId, int expGain)
    {
        if (expGain <= 0) return false;
        if (!_session.TryGetPlayer(accountId, out var role)) return false;

        int oldLevel = role.Level;
        role.Exp += expGain;

        _logger.LogInformation("[LevelUp] account={AccountId} +{Exp}exp, total={TotalExp}, level={Level}",
            accountId, expGain, role.Exp, role.Level);

        // 检查升级
        bool leveledUp = false;
        int maxLevel = _tables.GetMaxLevel();

        while (role.Level < maxLevel)
        {
            var nextLevelCfg = _tables.GetLevelUp(role.Level + 1);
            if (nextLevelCfg == null) break;
            if (role.Exp < nextLevelCfg.ExpRequired) break;

            // 升级！
            role.Level++;
            leveledUp = true;

            // 属性成长
            role.MaxHp += nextLevelCfg.Hp;
            role.MaxMp += nextLevelCfg.Mp;
            role.Patk += nextLevelCfg.Patk;
            role.Matk += nextLevelCfg.Matk;
            role.Pdef += nextLevelCfg.Pdef;
            role.Mdef += nextLevelCfg.Mdef;
            role.Agility += nextLevelCfg.Agility;

            // 升级后满血满蓝
            role.Hp = role.MaxHp;
            role.Mp = role.MaxMp;

            _logger.LogInformation("[LevelUp] account={AccountId} leveled up! {OldLevel} → {NewLevel}",
                accountId, oldLevel, role.Level);
        }

        // 同步到 MapPlayerState
        if (leveledUp)
        {
            var mapPlayer = _mapService.GetPlayerOnMap(role.CurrentMap, accountId);
            if (mapPlayer != null)
            {
                mapPlayer.Level = role.Level;
                mapPlayer.Hp = role.Hp;
                mapPlayer.MaxHp = role.MaxHp;
                mapPlayer.Mp = role.Mp;
                mapPlayer.MaxMp = role.MaxMp;
                mapPlayer.Patk = role.Patk;
                mapPlayer.Matk = role.Matk;
                mapPlayer.Pdef = role.Pdef;
                mapPlayer.Mdef = role.Mdef;
                mapPlayer.Agility = role.Agility;
            }
        }

        // 推送 RoleAttrNotify（包含最新等级/经验/属性）
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var roleInfo = PlayerProtoMapper.BuildRoleInfo(role, now);
        _network.SendToAccount(accountId, role.ServerId,
            (int)PProtocol.MessageId.GameRoleAttrNotify, roleInfo.ToByteArray());

        // 如果升级了，额外发升级通知
        if (leveledUp)
        {
            var notify = new PGame.LevelUpNotify
            {
                OldLevel = oldLevel,
                NewLevel = role.Level,
            };
            _network.SendToAccount(accountId, role.ServerId,
                (int)PProtocol.MessageId.GameLevelUpNotify, notify.ToByteArray());
        }

        return leveledUp;
    }

    /// <summary>
    /// 怪物死亡回调 — 给击杀者加经验
    /// </summary>
    public void OnMonsterDeath(long monsterInstanceId, long attackerId, int monsterId)
    {
        int expReward = _tables.GetMonsterExp(monsterId);
        if (expReward <= 0) return;

        _logger.LogInformation("[LevelUp] monster {MonsterId} killed by {AttackerId}, exp={Exp}",
            monsterId, attackerId, expReward);

        AddExp(attackerId, expReward);
    }
}
