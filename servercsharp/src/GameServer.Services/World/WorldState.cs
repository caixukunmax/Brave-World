using System.Collections.Concurrent;
using GameServer.Common.Config;
using GameServer.Services.Core;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.World;

/// <summary>
/// 移动预占记录
/// </summary>
public class MovementReservation
{
    public long EntityId { get; set; }
    public string MapName { get; set; } = "";
    public int FromX { get; set; }
    public int FromY { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public long StartTimeMs { get; set; }   // Environment.TickCount64
    public int DurationMs { get; set; }
    public int CheckRatio { get; set; }
    public int DualStartRatio { get; set; }
    public int DualEndRatio { get; set; }
    public bool Confirmed { get; set; }
    public bool Completed { get; set; }
    public bool CollisionPending { get; set; }
}

/// <summary>
/// ConfirmMove 结果
/// </summary>
public enum ConfirmResult
{
    Ok,         // 正常确认成功
    Collision,  // 碰撞：目标格有敌人
    Failed,     // 确认失败
}

/// <summary>
/// 世界状态 — 单一数据源，管理所有地图的玩家/怪物坐标 + 移动预占
/// </summary>
public class WorldState : IWorldState
{
    private readonly MapDataProvider _mapData;
    private readonly ILogger<WorldState> _logger;
    private readonly ConcurrentDictionary<string, MapState> _maps = new();
    private readonly ConcurrentDictionary<long, MovementReservation> _moveReservations = new();
    /// <summary>
    /// 已预占的格子。key = (mapName, x, y)，value = 占用者 entityId。
    /// 用 ConcurrentDictionary 替代 Dictionary&lt;string, HashSet&lt;...&gt;&gt;，避免 HashSet 非线程安全问题。
    /// </summary>
    private readonly ConcurrentDictionary<(string mapName, int x, int y), long> _reservedCells = new();

    /// <summary>
    /// 实体位置索引：entityId → (mapName, x, y)。
    /// 与 MapState.Players/Monsters/Npcs 同步维护，用于 O(1) 定位实体。
    /// </summary>
    private readonly Dictionary<long, (string mapName, int x, int y)> _entityLocations = new();

    public WorldState(MapDataProvider mapData, ILogger<WorldState> logger)
    {
        _mapData = mapData;
        _logger = logger;
        InitMaps();
    }

    private void InitMaps()
    {
        int mapId = 1;
        foreach (var (mapName, mapData) in _mapData.GetAllMaps())
        {
            _maps[mapName] = new MapState { MapId = mapId++ };
        }
        if (_maps.Count == 0)
            _maps[GameConstants.DefaultMapName] = new MapState { MapId = GameConstants.DefaultMapId };
    }

    // ---- 只读接口 ----

    public (string? mapName, (int x, int y)? pos) FindEntityPosition(long entityId)
    {
        if (_entityLocations.TryGetValue(entityId, out var loc))
            return (loc.mapName, (loc.x, loc.y));
        return (null, null);
    }

    public string GetEntityName(long entityId)
    {
        if (!_entityLocations.TryGetValue(entityId, out var loc))
            return $"entity_{entityId}";
        if (!_maps.TryGetValue(loc.mapName, out var map))
            return $"entity_{entityId}";
        if (map.Players.TryGetValue(entityId, out var p))
            return p.RoleName;
        if (map.Monsters.TryGetValue(entityId, out var m))
            return m.Name;
        if (map.Npcs.TryGetValue(entityId, out var n))
            return n.Name;
        return $"entity_{entityId}";
    }

    public ConcurrentDictionary<long, MapPlayerState> GetPlayersOnMap(string mapName)
        => _maps.TryGetValue(mapName, out var map) ? map.Players : new();

    public MapPlayerState? GetPlayerOnMap(string mapName, long accountId)
        => _maps.TryGetValue(mapName, out var map) && map.Players.TryGetValue(accountId, out var p) ? p : null;

    public ConcurrentDictionary<long, MapMonsterState> GetMonstersOnMap(string mapName)
        => _maps.TryGetValue(mapName, out var map) ? map.Monsters : new();

    public bool IsWalkable(string mapName, int x, int y) => _mapData.IsWalkable(mapName, x, y);

    public (int x, int y)? FindNearestWalkable(string mapName, int x, int y)
        => _mapData.FindNearestWalkable(mapName, x, y);

    public int GetTerrainType(string mapName, int x, int y)
        => _mapData.GetTerrainType(mapName, x, y);

    public int GetDecorationType(string mapName, int x, int y)
        => _mapData.GetDecorationType(mapName, x, y);

    public (int width, int height, int[,] decorationTypes)? GetMapDecorationData(string mapName)
        => _mapData.GetMapDecorationData(mapName);

    public bool IsOccupied(string mapName, int x, int y)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return false;
        if (map.GridEntities.TryGetValue((x, y), out var set) && set.Count > 0)
            return true;
        if (_reservedCells.ContainsKey((mapName, x, y))) return true;
        return false;
    }

    /// <summary>
    /// 获取指定格子上所有实体 ID（不含 excludedId）。
    /// </summary>
    private HashSet<long> GetEntitiesAt(string mapName, int x, int y, long excludedId)
    {
        if (!_maps.TryGetValue(mapName, out var map))
            return new HashSet<long>();
        if (!map.GridEntities.TryGetValue((x, y), out var set))
            return new HashSet<long>();
        var result = new HashSet<long>(set);
        result.Remove(excludedId);
        return result;
    }

    public ConcurrentDictionary<string, MapState> GetAllMaps() => _maps;

    public MapState? GetMapState(string mapName)
        => _maps.TryGetValue(mapName, out var inst) ? inst : null;

    public string? GetEntityMapName(long entityId)
    {
        return _entityLocations.TryGetValue(entityId, out var loc) ? loc.mapName : null;
    }

    // ---- 空间索引维护 ----

    private void AddEntityToSpatialIndex(long entityId, string mapName, int x, int y)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return;

        _entityLocations[entityId] = (mapName, x, y);
        if (!map.GridEntities.TryGetValue((x, y), out var set))
        {
            set = new HashSet<long>();
            map.GridEntities[(x, y)] = set;
        }
        set.Add(entityId);
    }

    private void RemoveEntityFromSpatialIndex(long entityId)
    {
        if (!_entityLocations.TryGetValue(entityId, out var loc))
            return;
        _entityLocations.Remove(entityId);

        if (_maps.TryGetValue(loc.mapName, out var map) &&
            map.GridEntities.TryGetValue((loc.x, loc.y), out var set))
        {
            set.Remove(entityId);
            if (set.Count == 0)
                map.GridEntities.Remove((loc.x, loc.y));
        }
    }

    private void MoveEntityInSpatialIndex(long entityId, string mapName, int newX, int newY)
    {
        RemoveEntityFromSpatialIndex(entityId);
        AddEntityToSpatialIndex(entityId, mapName, newX, newY);
    }

    // ---- 可变操作 ----

    public void PlayerEnter(string mapName, MapPlayerState player)
    {
        if (!_maps.TryGetValue(mapName, out var map))
        {
            map = new MapState { MapId = GameConstants.DefaultMapId };
            _maps[mapName] = map;
        }
        map.Players[player.AccountId] = player;
        AddEntityToSpatialIndex(player.AccountId, mapName, player.GridX, player.GridY);
    }

    public void PlayerMove(long accountId, string mapName, int x, int y)
    {
        if (_maps.TryGetValue(mapName, out var map) && map.Players.TryGetValue(accountId, out var p))
        {
            _logger.LogInformation("[WorldState] PlayerMove: account={AccountId} map={MapName} from=({FX},{FY}) to=({TX},{TY})",
                accountId, mapName, p.GridX, p.GridY, x, y);
            p.GridX = x;
            p.GridY = y;
            MoveEntityInSpatialIndex(accountId, mapName, x, y);
        }
        else
        {
            _logger.LogWarning("[WorldState] PlayerMove failed: account={AccountId} map={MapName} to=({TX},{TY}) — player not found",
                accountId, mapName, x, y);
        }
        // 清除旧移动预约（如 GM teleport），防止 GetCombatPositions 返回过时坐标
        _moveReservations.TryRemove(accountId, out _);
    }

    public void PlayerLeave(long accountId, string mapName)
    {
        if (_maps.TryGetValue(mapName, out var map) && map.Players.TryRemove(accountId, out _))
            RemoveEntityFromSpatialIndex(accountId);
    }

    public void MonsterEnter(string mapName, MapMonsterState monster)
    {
        if (!_maps.TryGetValue(mapName, out var map))
        {
            map = new MapState { MapId = GameConstants.DefaultMapId };
            _maps[mapName] = map;
        }
        map.Monsters[monster.InstanceId] = monster;
        AddEntityToSpatialIndex(monster.InstanceId, mapName, monster.X, monster.Y);
    }

    public void MonsterMove(long instanceId, string mapName, int x, int y)
    {
        if (_maps.TryGetValue(mapName, out var map) && map.Monsters.TryGetValue(instanceId, out var m))
        {
            m.X = x;
            m.Y = y;
            MoveEntityInSpatialIndex(instanceId, mapName, x, y);
        }
    }

    public void MonsterLeave(long instanceId, string mapName)
    {
        if (_maps.TryGetValue(mapName, out var map) && map.Monsters.TryRemove(instanceId, out _))
            RemoveEntityFromSpatialIndex(instanceId);
    }

    public void NpcEnter(string mapName, MapNpcState npc)
    {
        if (!_maps.TryGetValue(mapName, out var map))
        {
            map = new MapState { MapId = GameConstants.DefaultMapId };
            _maps[mapName] = map;
        }
        map.Npcs[npc.InstanceId] = npc;
        AddEntityToSpatialIndex(npc.InstanceId, mapName, npc.X, npc.Y);
    }

    // ---- 移动预占系统 ----

    public bool TryReserveMove(long entityId, string mapName, int fromX, int fromY, int targetX, int targetY,
        int durationMs, int checkRatio, int dualStartRatio, int dualEndRatio)
    {
        // 取消旧预约，避免同一实体同时占用多格
        if (_moveReservations.TryRemove(entityId, out var oldRes))
            _reservedCells.TryRemove((oldRes.MapName, oldRes.TargetX, oldRes.TargetY), out _);

        // 检查目标格是否被其他实体预定
        if (_reservedCells.ContainsKey((mapName, targetX, targetY)))
            return false;

        // 检查目标格是否有其他玩家/怪物/NPC（不含自己）
        if (_maps.TryGetValue(mapName, out var map) &&
            map.GridEntities.TryGetValue((targetX, targetY), out var occupants))
        {
            foreach (var id in occupants)
            {
                if (id == entityId) continue;
                // 其他玩家、怪物、NPC 均阻挡移动
                if (map.Players.ContainsKey(id) || map.Monsters.ContainsKey(id) || map.Npcs.ContainsKey(id))
                    return false;
            }
        }

        var res = new MovementReservation
        {
            EntityId = entityId,
            MapName = mapName,
            FromX = fromX,
            FromY = fromY,
            TargetX = targetX,
            TargetY = targetY,
            StartTimeMs = Environment.TickCount64,
            DurationMs = durationMs,
            CheckRatio = checkRatio,
            DualStartRatio = dualStartRatio,
            DualEndRatio = dualEndRatio,
        };

        _moveReservations[entityId] = res;
        _reservedCells[(mapName, targetX, targetY)] = entityId;
        return true;
    }

    /// <summary>
    /// 碰撞性移动预约：不检查目标格是否被占，不占格
    /// 但 NPC 格子仍然阻挡（NPC 不是敌人，不触发碰撞战斗）
    /// </summary>
    public bool TryReserveCollisionMove(long entityId, string mapName, int fromX, int fromY, int targetX, int targetY,
        int durationMs, int checkRatio, int dualStartRatio, int dualEndRatio)
    {
        // NPC 阻挡碰撞性移动（NPC 不是敌人）
        if (_maps.TryGetValue(mapName, out var map) &&
            map.GridEntities.TryGetValue((targetX, targetY), out var collisionOccupants))
        {
            foreach (var id in collisionOccupants)
                if (map.Npcs.ContainsKey(id))
                    return false;
        }

        // 取消旧预约
        if (_moveReservations.TryRemove(entityId, out var oldCollisionRes))
            _reservedCells.TryRemove((oldCollisionRes.MapName, oldCollisionRes.TargetX, oldCollisionRes.TargetY), out _);

        var res = new MovementReservation
        {
            EntityId = entityId,
            MapName = mapName,
            FromX = fromX,
            FromY = fromY,
            TargetX = targetX,
            TargetY = targetY,
            StartTimeMs = Environment.TickCount64,
            DurationMs = durationMs,
            CheckRatio = checkRatio,
            DualStartRatio = dualStartRatio,
            DualEndRatio = dualEndRatio,
            CollisionPending = true,
        };

        _moveReservations[entityId] = res;
        // 不加入 _reservedCells
        return true;
    }

    public bool ConfirmMove(long entityId)
    {
        var result = ConfirmMoveEx(entityId);
        return result == ConfirmResult.Ok;
    }

    /// <summary>
    /// 确认移动，返回详细结果（区分碰撞/失败/成功）
    /// </summary>
    public ConfirmResult ConfirmMoveEx(long entityId)
    {
        if (!_moveReservations.TryGetValue(entityId, out var res))
            return ConfirmResult.Failed;

        if (!res.CollisionPending)
        {
            // 普通预约：原有逻辑
            if (_maps.TryGetValue(res.MapName, out var map))
            {
                foreach (var p in map.Players.Values)
                    if (p.AccountId != entityId && p.GridX == res.TargetX && p.GridY == res.TargetY)
                    {
                        CancelMove(entityId);
                        return ConfirmResult.Failed;
                    }
                foreach (var m in map.Monsters.Values)
                    if (m.InstanceId != entityId && m.X == res.TargetX && m.Y == res.TargetY)
                    {
                        CancelMove(entityId);
                        return ConfirmResult.Failed;
                    }
            }

            UpdateEntityPosition(entityId, res);
            res.Confirmed = true;
            return ConfirmResult.Ok;
        }

        // 碰撞预约：检查目标格是否有敌人
        bool hasEnemy = false;
        if (_maps.TryGetValue(res.MapName, out var collisionMap))
        {
            foreach (var p in collisionMap.Players.Values)
                if (p.AccountId != entityId && p.GridX == res.TargetX && p.GridY == res.TargetY)
                    { hasEnemy = true; break; }
            if (!hasEnemy)
                foreach (var m in collisionMap.Monsters.Values)
                    if (m.InstanceId != entityId && m.X == res.TargetX && m.Y == res.TargetY)
                        { hasEnemy = true; break; }
        }

        CancelMove(entityId);

        if (hasEnemy)
            return ConfirmResult.Collision;

        return ConfirmResult.Failed;
    }

    private void UpdateEntityPosition(long entityId, MovementReservation res)
    {
        if (!_maps.TryGetValue(res.MapName, out var map)) return;
        if (map.Players.TryGetValue(entityId, out var p))
        {
            p.GridX = res.TargetX;
            p.GridY = res.TargetY;
        }
        if (map.Monsters.TryGetValue(entityId, out var m))
        {
            m.X = res.TargetX;
            m.Y = res.TargetY;
        }
    }

    /// <summary>
    /// 检查指定地图的目标格是否有敌对实体（用于碰撞通知校验）
    /// 敌对实体指玩家或怪物；NPC 不算敌对实体。
    /// </summary>
    public bool HasEnemyAt(string mapName, int targetX, int targetY, long excludeEntityId)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return false;
        if (!map.GridEntities.TryGetValue((targetX, targetY), out var set)) return false;
        foreach (var id in set)
        {
            if (id == excludeEntityId) continue;
            if (map.Players.ContainsKey(id) || map.Monsters.ContainsKey(id))
                return true;
        }
        return false;
    }

    public bool CompleteMove(long entityId)
    {
        if (!_moveReservations.TryGetValue(entityId, out var res))
            return false;

        res.Completed = true;
        CleanupReservation(entityId, res);
        return true;
    }

    public void CancelMove(long entityId)
    {
        if (!_moveReservations.TryGetValue(entityId, out var res))
            return;
        CleanupReservation(entityId, res);
    }

    private void CleanupReservation(long entityId, MovementReservation res)
    {
        _moveReservations.TryRemove(entityId, out _);
        _reservedCells.TryRemove((res.MapName, res.TargetX, res.TargetY), out _);
    }

    public MovementReservation? GetReservation(long entityId)
        => _moveReservations.GetValueOrDefault(entityId);

    /// <summary>
    /// 获取实体的权威坐标（地图服务/脱战等使用）
    /// </summary>
    public (string? mapName, (int x, int y)? pos) GetAuthorityPosition(long entityId)
    {
        var (mapName, pos) = FindEntityPosition(entityId);
        return (mapName, pos);
    }

    /// <summary>
    /// 获取实体的战斗位置（技能判定用）
    /// 返回多位置时，取最短距离
    /// </summary>
    public List<(string mapName, int x, int y)> GetCombatPositions(long entityId)
    {
        var result = new List<(string, int, int)>();
        var (authMap, authPos) = FindEntityPosition(entityId);

        if (authMap == null || authPos == null)
            return result;

        if (!_moveReservations.TryGetValue(entityId, out var res))
        {
            result.Add((authMap, authPos.Value.x, authPos.Value.y));
            return result;
        }

        var elapsed = Environment.TickCount64 - res.StartTimeMs;
        var progress = (int)(elapsed * 100 / res.DurationMs);

        if (progress < res.DualStartRatio)
        {
            // 还在原格
            result.Add((res.MapName, res.FromX, res.FromY));
        }
        else if (progress >= res.DualEndRatio)
        {
            // 已完全在新格
            result.Add((res.MapName, res.TargetX, res.TargetY));
        }
        else
        {
            // 双格区间
            result.Add((res.MapName, res.FromX, res.FromY));
            result.Add((res.MapName, res.TargetX, res.TargetY));
        }

        return result;
    }

    /// <summary>
    /// 清理已死亡/脱战实体的预占
    /// </summary>
    public void OnEntityDeathOrStun(long entityId)
    {
        CancelMove(entityId);
    }
}
