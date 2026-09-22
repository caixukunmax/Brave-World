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
    public int SizeX { get; set; } = 1;
    public int SizeY { get; set; } = 1;
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
    /// 实体位置索引：entityId → (mapName, anchorX, anchorY, sizeX, sizeY)。
    /// 与 MapState.Players/Monsters/Npcs 同步维护，用于 O(1) 定位实体。
    /// </summary>
    private readonly ConcurrentDictionary<long, (string mapName, int x, int y, int sizeX, int sizeY)> _entityLocations = new();

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
        {
            var defaultMap = _mapData.GetDefaultMap();
            if (defaultMap != null)
                _maps[defaultMap.Value.mapName] = new MapState { MapId = GameConstants.DefaultMapId };
        }
    }

    // ---- 只读接口 ----

    public (string? mapName, (int x, int y)? pos) FindEntityPosition(long entityId)
    {
        if (_entityLocations.TryGetValue(entityId, out var loc))
            return (loc.mapName, (loc.x, loc.y));
        return (null, null);
    }

    public (int sizeX, int sizeY) GetEntitySize(long entityId)
    {
        if (_entityLocations.TryGetValue(entityId, out var loc))
            return (Math.Max(1, loc.sizeX), Math.Max(1, loc.sizeY));
        return (1, 1);
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

    /// <summary>
    /// 查找最近的、能容纳指定 footprint 的锚点位置。
    /// 优先保证地形可行走；若 requireVacant 为 true，还会排除被其他实体占据或预约的格子。
    /// </summary>
    public (int x, int y)? FindNearestWalkableForFootprint(string mapName, int x, int y, int sizeX, int sizeY,
        int maxRadius = 10, bool requireVacant = true, long excludedEntityId = 0)
    {
        sizeX = Math.Max(1, sizeX);
        sizeY = Math.Max(1, sizeY);

        var candidate = _mapData.FindNearestWalkableForFootprint(mapName, x, y, sizeX, sizeY, maxRadius);
        if (candidate == null) return null;

        // 地形可行走的最近点若已被占用，尝试继续向外找一个既空旷又可行走的位置
        if (requireVacant && IsFootprintOccupiedByOther(mapName, candidate.Value.x, candidate.Value.y, sizeX, sizeY, excludedEntityId))
        {
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int ax = x + dx;
                        int ay = y + dy;
                        if (!_mapData.IsWalkable(mapName, ax, ay)) continue;
                        if (!IsFootprintWalkable(mapName, ax, ay, sizeX, sizeY)) continue;
                        if (!IsFootprintOccupiedByOther(mapName, ax, ay, sizeX, sizeY, excludedEntityId))
                            return (ax, ay);
                    }
                }
            }
            return null;
        }
        return candidate;
    }

    public int GetTerrainType(string mapName, int x, int y)
        => _mapData.GetTerrainType(mapName, x, y);

    public int GetDecorationType(string mapName, int x, int y)
        => _mapData.GetDecorationType(mapName, x, y);

    public (int width, int height, int[,] decorationTypes)? GetMapDecorationData(string mapName)
        => _mapData.GetMapDecorationData(mapName);

    public (int width, int height, int[,] terrainTypes)? GetMapTerrainData(string mapName)
        => _mapData.GetMapTerrainData(mapName);

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

    private void AddEntityToSpatialIndex(long entityId, string mapName, int anchorX, int anchorY, int sizeX, int sizeY)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return;

        sizeX = Math.Max(1, sizeX);
        sizeY = Math.Max(1, sizeY);
        _entityLocations[entityId] = (mapName, anchorX, anchorY, sizeX, sizeY);

        for (int dy = 0; dy < sizeY; dy++)
        {
            for (int dx = 0; dx < sizeX; dx++)
            {
                int gx = anchorX + dx;
                int gy = anchorY + dy;
                var key = (gx, gy);
                if (!map.GridEntities.TryGetValue(key, out var set))
                {
                    set = new HashSet<long>();
                    map.GridEntities[key] = set;
                }
                set.Add(entityId);
            }
        }
    }

    private void RemoveEntityFromSpatialIndex(long entityId)
    {
        if (!_entityLocations.TryGetValue(entityId, out var loc))
            return;
        _entityLocations.TryRemove(entityId, out _);

        if (!_maps.TryGetValue(loc.mapName, out var map))
            return;

        int sizeX = Math.Max(1, loc.sizeX);
        int sizeY = Math.Max(1, loc.sizeY);
        for (int dy = 0; dy < sizeY; dy++)
        {
            for (int dx = 0; dx < sizeX; dx++)
            {
                int gx = loc.x + dx;
                int gy = loc.y + dy;
                var key = (gx, gy);
                if (map.GridEntities.TryGetValue(key, out var set))
                {
                    set.Remove(entityId);
                    if (set.Count == 0)
                        map.GridEntities.Remove(key);
                }
            }
        }
    }

    private void MoveEntityInSpatialIndex(long entityId, string mapName, int newAnchorX, int newAnchorY)
    {
        if (!_entityLocations.TryGetValue(entityId, out var loc))
        {
            // 无历史 footprint，按 1×1 处理
            AddEntityToSpatialIndex(entityId, mapName, newAnchorX, newAnchorY, 1, 1);
            return;
        }
        RemoveEntityFromSpatialIndex(entityId);
        AddEntityToSpatialIndex(entityId, mapName, newAnchorX, newAnchorY, loc.sizeX, loc.sizeY);
    }

    /// <summary>获取实体当前占用的所有格子（基于空间索引）</summary>
    public List<(int x, int y)> GetEntityFootprint(long entityId)
    {
        var result = new List<(int x, int y)>();
        if (!_entityLocations.TryGetValue(entityId, out var loc))
            return result;
        if (!_maps.TryGetValue(loc.mapName, out var map))
            return result;

        int sizeX = Math.Max(1, loc.sizeX);
        int sizeY = Math.Max(1, loc.sizeY);
        for (int dy = 0; dy < sizeY; dy++)
            for (int dx = 0; dx < sizeX; dx++)
                result.Add((loc.x + dx, loc.y + dy));
        return result;
    }

    // ---- 可变操作 ----

    public void PlayerEnter(string mapName, MapPlayerState player)
    {
        // 幂等：若该账号已存在于某地图，先强制移除旧实体，避免脏空间索引
        if (_entityLocations.TryGetValue(player.AccountId, out var oldLoc))
        {
            _logger.LogWarning("[WorldState] PlayerEnter: account={AccountId} already on map={OldMap}, forcing leave before enter",
                player.AccountId, oldLoc.mapName);
            PlayerLeave(player.AccountId, oldLoc.mapName);
        }
        CancelMove(player.AccountId);

        if (!_maps.TryGetValue(mapName, out var map))
        {
            map = new MapState { MapId = GameConstants.DefaultMapId };
            _maps[mapName] = map;
        }
        map.Players[player.AccountId] = player;
        AddEntityToSpatialIndex(player.AccountId, mapName, player.GridX, player.GridY, player.SizeX, player.SizeY);
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
        AddEntityToSpatialIndex(monster.InstanceId, mapName, monster.X, monster.Y, monster.SizeX, monster.SizeY);
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
        AddEntityToSpatialIndex(npc.InstanceId, mapName, npc.X, npc.Y, npc.SizeX, npc.SizeY);
    }

    // ---- 移动预占系统 ----

    private static IEnumerable<(int x, int y)> GetFootprintCells(int anchorX, int anchorY, int sizeX, int sizeY)
    {
        sizeX = Math.Max(1, sizeX);
        sizeY = Math.Max(1, sizeY);
        for (int dy = 0; dy < sizeY; dy++)
            for (int dx = 0; dx < sizeX; dx++)
                yield return (anchorX + dx, anchorY + dy);
    }

    private bool IsFootprintWalkable(string mapName, int anchorX, int anchorY, int sizeX, int sizeY)
    {
        foreach (var (x, y) in GetFootprintCells(anchorX, anchorY, sizeX, sizeY))
        {
            if (!_mapData.IsWalkable(mapName, x, y))
                return false;
        }
        return true;
    }

    /// <summary>检查 footprint 内是否有其他玩家/怪物/NPC（不含 excludedId）</summary>
    private bool IsFootprintOccupiedByOther(string mapName, int anchorX, int anchorY, int sizeX, int sizeY, long excludedId)
    {
        if (!_maps.TryGetValue(mapName, out var map))
            return false;

        foreach (var (x, y) in GetFootprintCells(anchorX, anchorY, sizeX, sizeY))
        {
            if (!map.GridEntities.TryGetValue((x, y), out var occupants))
                continue;
            foreach (var id in occupants)
            {
                if (id == excludedId) continue;
                if (map.Players.ContainsKey(id) || map.Monsters.ContainsKey(id) || map.Npcs.ContainsKey(id))
                    return true;
            }
        }
        return false;
    }

    /// <summary>检查 footprint 内是否有其他实体的预约（不含 excludedId）</summary>
    private bool IsFootprintReservedByOther(string mapName, int anchorX, int anchorY, int sizeX, int sizeY, long excludedId)
    {
        foreach (var (x, y) in GetFootprintCells(anchorX, anchorY, sizeX, sizeY))
        {
            if (_reservedCells.TryGetValue((mapName, x, y), out var occupant) && occupant != excludedId)
                return true;
        }
        return false;
    }

    private (int sizeX, int sizeY) GetMovingEntitySize(long entityId)
    {
        return GetEntitySize(entityId);
    }

    public bool TryReserveMove(long entityId, string mapName, int fromX, int fromY, int targetX, int targetY,
        int durationMs, int checkRatio, int dualStartRatio, int dualEndRatio)
    {
        var (sizeX, sizeY) = GetMovingEntitySize(entityId);

        // 取消旧预约
        if (_moveReservations.TryRemove(entityId, out var oldRes))
            RemoveReservedCells(oldRes);

        // 检查新 footprint 内所有格子可行走
        if (!IsFootprintWalkable(mapName, targetX, targetY, sizeX, sizeY))
            return false;

        // 检查新 footprint 是否被其他实体占据
        if (IsFootprintOccupiedByOther(mapName, targetX, targetY, sizeX, sizeY, entityId))
            return false;

        // 检查新 footprint 是否被其他实体预约
        if (IsFootprintReservedByOther(mapName, targetX, targetY, sizeX, sizeY, entityId))
            return false;

        var res = new MovementReservation
        {
            EntityId = entityId,
            MapName = mapName,
            FromX = fromX,
            FromY = fromY,
            TargetX = targetX,
            TargetY = targetY,
            SizeX = sizeX,
            SizeY = sizeY,
            StartTimeMs = Environment.TickCount64,
            DurationMs = durationMs,
            CheckRatio = checkRatio,
            DualStartRatio = dualStartRatio,
            DualEndRatio = dualEndRatio,
        };

        _moveReservations[entityId] = res;
        AddReservedCells(res);
        return true;
    }

    private void AddReservedCells(MovementReservation res)
    {
        int sizeX = Math.Max(1, res.SizeX);
        int sizeY = Math.Max(1, res.SizeY);
        foreach (var (x, y) in GetFootprintCells(res.TargetX, res.TargetY, sizeX, sizeY))
            _reservedCells[(res.MapName, x, y)] = res.EntityId;
    }

    private void RemoveReservedCells(MovementReservation res)
    {
        int sizeX = Math.Max(1, res.SizeX);
        int sizeY = Math.Max(1, res.SizeY);
        foreach (var (x, y) in GetFootprintCells(res.TargetX, res.TargetY, sizeX, sizeY))
            _reservedCells.TryRemove((res.MapName, x, y), out _);
    }

    /// <summary>
    /// 碰撞性移动预约：目标 footprint 内不能包含 NPC；不占用预约格。
    /// </summary>
    public bool TryReserveCollisionMove(long entityId, string mapName, int fromX, int fromY, int targetX, int targetY,
        int durationMs, int checkRatio, int dualStartRatio, int dualEndRatio)
    {
        var (sizeX, sizeY) = GetMovingEntitySize(entityId);

        // 取消旧普通/碰撞预约
        if (_moveReservations.TryRemove(entityId, out var oldRes))
            RemoveReservedCells(oldRes);

        // NPC 阻挡碰撞性移动（NPC 不是敌人）
        if (_maps.TryGetValue(mapName, out var map))
        {
            foreach (var (x, y) in GetFootprintCells(targetX, targetY, sizeX, sizeY))
            {
                if (!map.GridEntities.TryGetValue((x, y), out var occupants))
                    continue;
                foreach (var id in occupants)
                    if (map.Npcs.ContainsKey(id))
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
            SizeX = sizeX,
            SizeY = sizeY,
            StartTimeMs = Environment.TickCount64,
            DurationMs = durationMs,
            CheckRatio = checkRatio,
            DualStartRatio = dualStartRatio,
            DualEndRatio = dualEndRatio,
            CollisionPending = true,
        };

        _moveReservations[entityId] = res;
        // 碰撞预约不加入 _reservedCells
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

        int sizeX = Math.Max(1, res.SizeX);
        int sizeY = Math.Max(1, res.SizeY);

        if (!res.CollisionPending)
        {
            // 普通预约：检查目标 footprint 是否与其他实体重叠
            if (IsFootprintOccupiedByOther(res.MapName, res.TargetX, res.TargetY, sizeX, sizeY, entityId))
            {
                CancelMove(entityId);
                return ConfirmResult.Failed;
            }

            UpdateEntityPosition(entityId, res);
            res.Confirmed = true;
            return ConfirmResult.Ok;
        }

        // 碰撞预约：检查目标 footprint 是否与敌人 footprint 重叠
        bool hasEnemy = IsEnemyFootprintOverlapping(res.MapName, res.TargetX, res.TargetY, sizeX, sizeY, entityId);

        CancelMove(entityId);

        if (hasEnemy)
            return ConfirmResult.Collision;

        return ConfirmResult.Failed;
    }

    private bool IsEnemyFootprintOverlapping(string mapName, int anchorX, int anchorY, int sizeX, int sizeY, long excludeEntityId)
    {
        if (!_maps.TryGetValue(mapName, out var map))
            return false;

        foreach (var (x, y) in GetFootprintCells(anchorX, anchorY, sizeX, sizeY))
        {
            if (!map.GridEntities.TryGetValue((x, y), out var occupants))
                continue;
            foreach (var id in occupants)
            {
                if (id == excludeEntityId) continue;
                if (map.Players.ContainsKey(id) || map.Monsters.ContainsKey(id))
                    return true;
            }
        }
        return false;
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
        MoveEntityInSpatialIndex(entityId, res.MapName, res.TargetX, res.TargetY);
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
        RemoveReservedCells(res);
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
    /// 无移动预约时返回当前 footprint 所有格子；移动中返回旧/新 footprint 并集。
    /// </summary>
    public List<(string mapName, int x, int y)> GetCombatPositions(long entityId)
    {
        var result = new List<(string, int, int)>();
        var (authMap, authPos) = FindEntityPosition(entityId);

        if (authMap == null || authPos == null)
            return result;

        var (sizeX, sizeY) = GetEntitySize(entityId);

        if (!_moveReservations.TryGetValue(entityId, out var res))
        {
            foreach (var (x, y) in GetFootprintCells(authPos.Value.x, authPos.Value.y, sizeX, sizeY))
                result.Add((authMap, x, y));
            return result;
        }

        var elapsed = Environment.TickCount64 - res.StartTimeMs;
        var progress = (int)(elapsed * 100 / res.DurationMs);

        if (progress < res.DualStartRatio)
        {
            // 还在原格
            foreach (var (x, y) in GetFootprintCells(res.FromX, res.FromY, sizeX, sizeY))
                result.Add((res.MapName, x, y));
        }
        else if (progress >= res.DualEndRatio)
        {
            // 已完全在新格
            foreach (var (x, y) in GetFootprintCells(res.TargetX, res.TargetY, sizeX, sizeY))
                result.Add((res.MapName, x, y));
        }
        else
        {
            // 双格区间：旧 footprint + 新 footprint 并集
            var set = new HashSet<(int x, int y)>();
            foreach (var cell in GetFootprintCells(res.FromX, res.FromY, sizeX, sizeY))
                set.Add(cell);
            foreach (var cell in GetFootprintCells(res.TargetX, res.TargetY, sizeX, sizeY))
                set.Add(cell);
            foreach (var (x, y) in set)
                result.Add((res.MapName, x, y));
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
