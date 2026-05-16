using GameServer.Common.Config;
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
/// 玩家死亡响应器 — 监听 CombatManager.DeathCallback 执行重生流程
/// </summary>
public class DeathResponder
{
    private readonly ILogger<DeathResponder> _logger;
    private readonly MapService _mapService;
    private readonly MapDataProvider _mapData;
    private readonly PlayerSessionManager _session;
    private readonly LubanTableLoader _tables;
    private readonly INetworkSender _network;

    public DeathResponder(
        ILogger<DeathResponder> logger,
        MapService mapService,
        MapDataProvider mapData,
        PlayerSessionManager session,
        LubanTableLoader tables,
        INetworkSender network)
    {
        _logger = logger;
        _mapService = mapService;
        _mapData = mapData;
        _session = session;
        _tables = tables;
        _network = network;
    }

    /// <summary>
    /// 绑定到 CombatManager.DeathCallback
    /// </summary>
    public void OnPlayerDeath(long entityId, Dictionary<string, MapState> maps)
    {
        _logger.LogInformation("[DeathResponder] player death: account={AccountId}", entityId);

        if (!_session.TryGetPlayer(entityId, out var role))
        {
            _logger.LogWarning("[DeathResponder] player not found: {AccountId}", entityId);
            return;
        }

        string currentMap = role.CurrentMap;

        // 1. 取消移动
        _mapService.World.OnEntityDeathOrStun(entityId);

        // 2. 离开地图（从旧位置移除）
        _mapService.PlayerLeave(entityId, currentMap);

        // 3. 获取出生点
        var (spawnX, spawnY) = _mapData.GetSpawnPoint(currentMap);
        var walkable = _mapData.FindNearestWalkable(currentMap, spawnX, spawnY);
        if (walkable != null) { spawnX = walkable.Value.x; spawnY = walkable.Value.y; }

        // 4. 恢复满血（按当前等级计算属性）
        var (hp, mp, patk, matk, pdef, mdef, _) = _tables.GetPlayerAttrsByLevel(role.Level);

        // 5. 更新 Role 模型
        role.GridX = spawnX;
        role.GridY = spawnY;

        // 6. 重新进入地图（满血）
        _mapService.PlayerEnter(new PlayerSnapshot
        {
            AccountId = entityId,
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            ServerId = role.ServerId,
            GridX = spawnX,
            GridY = spawnY,
            Level = role.Level,
            CurrentMap = currentMap,
            Hp = hp, MaxHp = hp, Mp = mp, MaxMp = mp,
            Patk = patk, Matk = matk, Pdef = pdef, Mdef = mdef,
            MpRegen = role.MpRegen,
        });

        // 7. 发送死亡通知给客户端
        var notify = new PGame.PlayerDeathNotify
        {
            SpawnX = spawnX,
            SpawnY = spawnY,
            Hp = hp,
            MaxHp = hp,
            Mp = mp,
            MaxMp = mp,
        };
        _network.SendToAccount(entityId, role.ServerId,
            (int)PProtocol.MessageId.GamePlayerDeathNotify, notify.ToByteArray());

        // 8. 持久化
        _ = _session.Roles.Update(role.RoleId, u => u
            .Set(r => r.GridX, spawnX)
            .Set(r => r.GridY, spawnY));

        _logger.LogInformation("[DeathResponder] respawned account={AccountId} at ({X},{Y}) hp={Hp}",
            entityId, spawnX, spawnY, hp);
    }
}
