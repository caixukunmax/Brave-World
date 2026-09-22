using GameServer.Common.Buffs;
using GameServer.Common.Events;
using System.Collections.Concurrent;
using GameServer.Services.Core;
using GameServer.Services.World;
using GameServer.Tables;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using INetworkSender = GameServer.Services.Core.INetworkSender;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Map;

/// <summary>
/// 地图服务 — 移植自 map_pool/service.lua
/// 委托 WorldState 管理坐标，CollisionDetector 发布碰撞事件
/// </summary>
public class MapService
{
    private readonly ILogger<MapService> _logger;
    private readonly WorldState _worldState;
    private readonly CollisionDetector _collision;
    private readonly INetworkSender _network;
    private readonly LubanTableLoader _tables;

    public MapService(ILogger<MapService> logger, WorldState worldState, CollisionDetector collision, INetworkSender network, LubanTableLoader tables)
    {
        _logger = logger;
        _worldState = worldState;
        _collision = collision;
        _network = network;
        _tables = tables;
    }

    // 暴露 WorldState 供需要旧接口的地方使用
    public WorldState World => _worldState;

    // ---- 玩家 ----

    public void PlayerEnter(PlayerSnapshot snapshot)
    {
        _worldState.PlayerEnter(snapshot.CurrentMap, new MapPlayerState
        {
            AccountId = snapshot.AccountId,
            RoleId = snapshot.RoleId,
            RoleName = snapshot.RoleName,
            ServerId = snapshot.ServerId,
            GridX = snapshot.GridX,
            GridY = snapshot.GridY,
            SizeX = snapshot.SizeX > 0 ? snapshot.SizeX : 1,
            SizeY = snapshot.SizeY > 0 ? snapshot.SizeY : 1,
            Level = snapshot.Level,
            Hp = snapshot.Hp,
            MaxHp = snapshot.MaxHp,
            Mp = snapshot.Mp,
            MaxMp = snapshot.MaxMp,
            Patk = snapshot.Patk,
            Matk = snapshot.Matk,
            Pdef = snapshot.Pdef,
            Mdef = snapshot.Mdef,
            MpRegen = snapshot.MpRegen,
            Job = snapshot.Job,
            MoveSpeedMs = snapshot.MoveSpeedMs,
            PreferredSkillId = snapshot.PreferredSkillId,
            EquippedSkills = snapshot.EquippedSkills,
            Buffs = new BuffContainer(_tables),
        });
        _logger.LogInformation("PlayerEnter: account={AccountId} map={Map} pos=({X},{Y})",
            snapshot.AccountId, snapshot.CurrentMap, snapshot.GridX, snapshot.GridY);
    }

    public void PlayerMove(long accountId, string mapName, int x, int y)
    {
        _worldState.PlayerMove(accountId, mapName, x, y);
    }

    public void PlayerLeave(long accountId, string mapName)
    {
        _worldState.PlayerLeave(accountId, mapName);
    }

    // ---- 怪物 ----

    public void MonsterEnter(long instanceId, int monsterId, string mapName, string name, int x, int y,
        int hp, int maxHp, int level, int patk, int matk, int pdef, int mdef, int sizeX = 1, int sizeY = 1)
    {
        _worldState.MonsterEnter(mapName, new MapMonsterState
        {
            InstanceId = instanceId,
            MonsterId = monsterId,
            Name = name,
            X = x,
            Y = y,
            SizeX = sizeX > 0 ? sizeX : 1,
            SizeY = sizeY > 0 ? sizeY : 1,
            Hp = hp,
            MaxHp = maxHp,
            Level = level,
            Patk = patk,
            Matk = matk,
            Pdef = pdef,
            Mdef = mdef,
        });
    }

    public void MonsterMove(long instanceId, string mapName, int x, int y)
    {
        _worldState.MonsterMove(instanceId, mapName, x, y);
    }

    /// <summary>
    /// 统一碰撞检测：实体到达 (x,y) 后检查相邻敌方实体
    /// 所有实体类型（玩家、怪物、未来新类型）共用
    /// </summary>
    public void CheckEntityCollision(long entityId, string mapName, int x, int y)
    {
        var maps = _worldState.GetAllMaps();
        if (maps.TryGetValue(mapName, out var map))
            _collision.CheckEntityCollision(entityId, mapName, x, y, map);
    }

    // ---- 查询/广播 ----

    public bool IsWalkable(string mapName, int x, int y) => _worldState.IsWalkable(mapName, x, y);
    public (int x, int y)? FindNearestWalkable(string mapName, int x, int y) => _worldState.FindNearestWalkable(mapName, x, y);
    public (int x, int y)? FindNearestWalkableForFootprint(string mapName, int x, int y, int sizeX, int sizeY)
        => _worldState.FindNearestWalkableForFootprint(mapName, x, y, sizeX, sizeY);
    public bool IsOccupied(string mapName, int x, int y) => _worldState.IsOccupied(mapName, x, y);
    public int GetTerrainType(string mapName, int x, int y) => _worldState.GetTerrainType(mapName, x, y);
    public int GetDecorationType(string mapName, int x, int y) => _worldState.GetDecorationType(mapName, x, y);
    public (int width, int height, int[,] decorationTypes)? GetMapDecorationData(string mapName)
        => _worldState.GetMapDecorationData(mapName);
    public float GetTerrainMoveSpeedRatio(string mapName, int x, int y)
    {
        var terrainId = _worldState.GetTerrainType(mapName, x, y);
        var cfg = _tables.GetTerrainConfig(terrainId);
        return cfg?.MoveSpeedRatio ?? 1.0f;
    }
    public (int width, int height, int[,] terrainTypes)? GetMapTerrainData(string mapName)
        => _worldState.GetMapTerrainData(mapName);

    public ConcurrentDictionary<long, MapPlayerState> GetPlayersOnMap(string mapName)
    {
        return _worldState.GetPlayersOnMap(mapName);
    }

    /// <summary>获取地图上指定玩家的运行时状态（可直接修改属性，战斗系统实时生效）</summary>
    public MapPlayerState? GetPlayerOnMap(string mapName, long accountId)
    {
        return _worldState.GetPlayerOnMap(mapName, accountId);
    }

    public void BroadcastToMap(string mapName, int msgId, byte[] data)
    {
        var players = _worldState.GetPlayersOnMap(mapName);
        foreach (var p in players.Values)
            _network.SendToAccount(p.AccountId, p.ServerId, msgId, data);
    }

    // ---- 快照接口（直接引用权威数据，不深拷贝） ----

    /// <summary>
    /// 获取地图快照 — 返回直接引用权威 WorldState 数据的 Dictionary。
    /// 战斗系统直接修改 MapPlayerState/MapMonsterState 的 HP/MP/Buff/InCombat，
    /// 无需事后 SyncCombatHp。注意：调用方不应增删 Players/Monsters 字典条目。
    /// </summary>
    public Dictionary<string, MapState> GetMapsSnapshot()
    {
        var result = new Dictionary<string, MapState>();
        foreach (var (name, instance) in _worldState.GetAllMaps())
            result[name] = instance;
        return result;
    }

    /// <summary>
    /// 刷新快照中所有实体的 CombatPositions（有活跃移动预约时更新为双格区间）。
    /// 替代原先 GetAllMapsLegacy 中从 WorldState.GetCombatPositions 获取位置并存入副本的逻辑。
    /// 由于快照直接引用权威数据，只需刷新 CombatPositions 字段即可。
    /// </summary>
    public void RefreshCombatPositionsForSnapshot(Dictionary<string, MapState> snapshot)
    {
        foreach (var (mapName, map) in snapshot)
        {
            foreach (var (id, p) in map.Players)
            {
                var combatPos = _worldState.GetCombatPositions(id)
                    .Where(cp => cp.mapName == mapName)
                    .Select(cp => (cp.x, cp.y))
                    .ToList();
                p.CombatPositions = combatPos.Count > 0 ? combatPos
                    : new List<(int, int)> { (p.GridX, p.GridY) };
            }
            foreach (var (id, m) in map.Monsters)
            {
                var combatPos = _worldState.GetCombatPositions(id)
                    .Where(cp => cp.mapName == mapName)
                    .Select(cp => (cp.x, cp.y))
                    .ToList();
                m.CombatPositions = combatPos.Count > 0 ? combatPos
                    : new List<(int, int)> { (m.X, m.Y) };
            }
        }
    }

    /// <summary>
    /// 非战斗状态的 buff 过期检查 + 推送
    /// 战斗中的 buff 由 CombatManager.TickBuffs 处理
    /// </summary>
    public void TickOutOfCombatBuffs(Dictionary<string, MapState> maps)
    {
        var now = Environment.TickCount64;
        var changedPlayers = new List<(long AccountId, int ServerId, MapPlayerState Player)>();

        foreach (var map in maps.Values)
        {
            foreach (var (id, p) in map.Players)
            {
                if (p.Buffs.Buffs.Count == 0) continue;
                // 战斗中的 buff 由 CombatManager.TickBuffs 处理，跳过
                if (p.InCombat) continue;

                bool changed = false;
                for (int i = p.Buffs.Buffs.Count - 1; i >= 0; i--)
                {
                    var buff = p.Buffs.Buffs[i];
                    if (buff.ExpireTime > 0 && now >= buff.ExpireTime)
                    {
                        p.Buffs.RemoveBuff(buff.BuffId);
                        changed = true;
                    }
                }

                if (changed)
                    changedPlayers.Add((id, p.ServerId, p));
            }
        }

        // 推送过期通知
        foreach (var (accountId, serverId, p) in changedPlayers)
        {
            var notify = new PGame.BuffUpdateNotify { EntityId = (ulong)accountId };
            foreach (var b in p.Buffs.Buffs)
            {
                var cfg = _tables.GetBuff(b.BuffId);
                string bName = cfg?.Name ?? $"Buff{b.BuffId}";
                float remaining = b.ExpireTime <= 0 ? -1f : (float)(b.ExpireTime - now) / 1000f;
                if (remaining < 0 && b.ExpireTime > 0) remaining = 0;
                notify.Buffs.Add(new PGame.BuffUpdateNotify.Types.BuffEntry
                {
                    BuffId = b.BuffId, BuffName = bName, Stacks = b.Stacks,
                    RemainingTime = remaining, ShieldAmount = b.ShieldRemaining,
                });
            }
            _network.SendToAccount(accountId, serverId,
                (int)PProtocol.MessageId.GameBuffUpdateNotify, notify.ToByteArray());
        }
    }
}

// ---- 旧状态类型（向后兼容，后续 Phase 删除）----

public class PlayerSnapshot
{
    public long AccountId { get; set; }
    public long RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public int ServerId { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int SizeX { get; set; } = 1;
    public int SizeY { get; set; } = 1;
    public int Level { get; set; }
    public string CurrentMap { get; set; } = "xinshoucun";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public int Agility { get; set; }
    public int Patk { get; set; }
    public int Matk { get; set; }
    public int Pdef { get; set; }
    public int Mdef { get; set; }
    public int MpRegen { get; set; }
    public string Job { get; set; } = "";
    public int MoveSpeedMs { get; set; }
    public int PreferredSkillId { get; set; }
    public List<int> EquippedSkills { get; set; } = new();
}


