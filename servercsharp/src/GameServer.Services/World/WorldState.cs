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
    private readonly ConcurrentDictionary<string, HashSet<(int x, int y)>> _reservedCells = new();

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
        foreach (var (mapName, map) in _maps)
        {
            if (map.Players.TryGetValue(entityId, out var p))
                return (mapName, (p.GridX, p.GridY));
            if (map.Monsters.TryGetValue(entityId, out var m))
                return (mapName, (m.X, m.Y));
        }
        return (null, null);
    }

    public string GetEntityName(long entityId)
    {
        foreach (var map in _maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
                return p.RoleName;
            if (map.Monsters.TryGetValue(entityId, out var m))
                return m.Name;
            if (map.Npcs.TryGetValue(entityId, out var n))
                return n.Name;
        }
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

    public bool IsOccupied(string mapName, int x, int y)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return false;
        foreach (var m in map.Monsters.Values)
            if (m.X == x && m.Y == y) return true;
        foreach (var p in map.Players.Values)
            if (p.GridX == x && p.GridY == y) return true;
        foreach (var n in map.Npcs.Values)
            if (n.X == x && n.Y == y) return true;
        if (_reservedCells.TryGetValue(mapName, out var cells))
            if (cells.Contains((x, y))) return true;
        return false;
    }

    public ConcurrentDictionary<string, MapState> GetAllMaps() => _maps;

    public MapState? GetMapState(string mapName)
        => _maps.TryGetValue(mapName, out var inst) ? inst : null;

    public string? GetEntityMapName(long entityId)
    {
        foreach (var (mapName, map) in _maps)
        {
            if (map.Players.ContainsKey(entityId)) return mapName;
            if (map.Monsters.ContainsKey(entityId)) return mapName;
        }
        return null;
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
    }

    public void PlayerMove(long accountId, string mapName, int x, int y)
    {
        if (_maps.TryGetValue(mapName, out var map) && map.Players.TryGetValue(accountId, out var p))
        {
            _logger.LogInformation("[WorldState] PlayerMove: account={AccountId} map={MapName} from=({FX},{FY}) to=({TX},{TY})",
                accountId, mapName, p.GridX, p.GridY, x, y);
            p.GridX = x;
            p.GridY = y;
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
        if (_maps.TryGetValue(mapName, out var map))
            map.Players.TryRemove(accountId, out _);
    }

    public void MonsterEnter(string mapName, MapMonsterState monster)
    {
        if (!_maps.TryGetValue(mapName, out var map))
        {
            map = new MapState { MapId = GameConstants.DefaultMapId };
            _maps[mapName] = map;
        }
        map.Monsters[monster.InstanceId] = monster;
    }

    public void MonsterMove(long instanceId, string mapName, int x, int y)
    {
        if (_maps.TryGetValue(mapName, out var map) && map.Monsters.TryGetValue(instanceId, out var m))
        {
            m.X = x;
            m.Y = y;
        }
    }

    public void MonsterLeave(long instanceId, string mapName)
    {
        if (_maps.TryGetValue(mapName, out var map))
            map.Monsters.TryRemove(instanceId, out _);
    }

    // ---- 移动预占系统 ----

    public bool TryReserveMove(long entityId, string mapName, int fromX, int fromY, int targetX, int targetY,
        int durationMs, int checkRatio, int dualStartRatio, int dualEndRatio)
    {
        if (_moveReservations.ContainsKey(entityId))
            CancelMove(entityId);

        if (!_reservedCells.TryGetValue(mapName, out var cells))
        {
            cells = new HashSet<(int, int)>();
            _reservedCells[mapName] = cells;
        }

        // 检查目标格是否被预定
        if (cells.Contains((targetX, targetY)))
            return false;

        // 检查目标格是否有其他玩家/怪物/NPC（不含自己）
        if (_maps.TryGetValue(mapName, out var map))
        {
            foreach (var p in map.Players.Values)
                if (p.AccountId != entityId && p.GridX == targetX && p.GridY == targetY)
                    return false;
            foreach (var m in map.Monsters.Values)
                if (m.InstanceId != entityId && m.X == targetX && m.Y == targetY)
                    return false;
            // NPC 阻挡移动（像墙壁一样）
            foreach (var n in map.Npcs.Values)
                if (n.X == targetX && n.Y == targetY)
                    return false;
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
        cells.Add((targetX, targetY));
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
        if (_maps.TryGetValue(mapName, out var map))
        {
            foreach (var n in map.Npcs.Values)
                if (n.X == targetX && n.Y == targetY)
                    return false;
        }

        if (_moveReservations.ContainsKey(entityId))
            CancelMove(entityId);

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
    /// </summary>
    public bool HasEnemyAt(string mapName, int targetX, int targetY, long excludeEntityId)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return false;
        foreach (var p in map.Players.Values)
            if (p.AccountId != excludeEntityId && p.GridX == targetX && p.GridY == targetY)
                return true;
        foreach (var m in map.Monsters.Values)
            if (m.InstanceId != excludeEntityId && m.X == targetX && m.Y == targetY)
                return true;
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
        if (_reservedCells.TryGetValue(res.MapName, out var cells))
            cells.Remove((res.TargetX, res.TargetY));
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
