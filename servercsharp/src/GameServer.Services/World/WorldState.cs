using GameServer.Common.Config;
using GameServer.Services.Core;

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
}

/// <summary>
/// 世界状态 — 单一数据源，管理所有地图的玩家/怪物坐标 + 移动预占
/// </summary>
public class WorldState : IWorldState
{
    private readonly MapDataProvider _mapData;
    private readonly Dictionary<string, MapInstance> _maps = new();
    private readonly Dictionary<long, MovementReservation> _moveReservations = new();
    private readonly Dictionary<string, HashSet<(int x, int y)>> _reservedCells = new();

    public WorldState(MapDataProvider mapData)
    {
        _mapData = mapData;
        InitMaps();
    }

    private void InitMaps()
    {
        int mapId = 1;
        foreach (var (mapName, mapData) in _mapData.GetAllMaps())
        {
            _maps[mapName] = new MapInstance { MapId = mapId++ };
        }
        if (_maps.Count == 0)
            _maps[GameConstants.DefaultMapName] = new MapInstance { MapId = GameConstants.DefaultMapId };
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
                return $"monster_{m.InstanceId}";
        }
        return $"entity_{entityId}";
    }

    public Dictionary<long, MapPlayerState> GetPlayersOnMap(string mapName)
        => _maps.TryGetValue(mapName, out var map) ? map.Players : new();

    public MapPlayerState? GetPlayerOnMap(string mapName, long accountId)
        => _maps.TryGetValue(mapName, out var map) && map.Players.TryGetValue(accountId, out var p) ? p : null;

    public Dictionary<long, MapMonsterState> GetMonstersOnMap(string mapName)
        => _maps.TryGetValue(mapName, out var map) ? map.Monsters : new();

    public bool IsWalkable(string mapName, int x, int y) => _mapData.IsWalkable(mapName, x, y);

    public (int x, int y)? FindNearestWalkable(string mapName, int x, int y)
        => _mapData.FindNearestWalkable(mapName, x, y);

    public bool IsOccupied(string mapName, int x, int y)
    {
        if (!_maps.TryGetValue(mapName, out var map)) return false;
        foreach (var m in map.Monsters.Values)
            if (m.X == x && m.Y == y) return true;
        foreach (var p in map.Players.Values)
            if (p.GridX == x && p.GridY == y) return true;
        if (_reservedCells.TryGetValue(mapName, out var cells))
            if (cells.Contains((x, y))) return true;
        return false;
    }

    public Dictionary<string, MapInstance> GetAllMaps() => _maps;

    public MapInstance? GetMapInstance(string mapName)
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
            map = new MapInstance { MapId = GameConstants.DefaultMapId };
            _maps[mapName] = map;
        }
        map.Players[player.AccountId] = player;
    }

    public void PlayerMove(long accountId, string mapName, int x, int y)
    {
        if (_maps.TryGetValue(mapName, out var map) && map.Players.TryGetValue(accountId, out var p))
        {
            p.GridX = x;
            p.GridY = y;
        }
    }

    public void PlayerLeave(long accountId, string mapName)
    {
        if (_maps.TryGetValue(mapName, out var map))
            map.Players.Remove(accountId);
    }

    public void MonsterEnter(string mapName, MapMonsterState monster)
    {
        if (!_maps.TryGetValue(mapName, out var map))
        {
            map = new MapInstance { MapId = GameConstants.DefaultMapId };
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
            map.Monsters.Remove(instanceId);
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

        // 检查目标格是否有其他玩家/怪物（不含自己）
        if (_maps.TryGetValue(mapName, out var map))
        {
            foreach (var p in map.Players.Values)
                if (p.AccountId != entityId && p.GridX == targetX && p.GridY == targetY)
                    return false;
            foreach (var m in map.Monsters.Values)
                if (m.InstanceId != entityId && m.X == targetX && m.Y == targetY)
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

    public bool ConfirmMove(long entityId)
    {
        if (!_moveReservations.TryGetValue(entityId, out var res))
            return false;

        if (_maps.TryGetValue(res.MapName, out var map))
        {
            // 再次检查目标格
            foreach (var p in map.Players.Values)
                if (p.AccountId != entityId && p.GridX == res.TargetX && p.GridY == res.TargetY)
                {
                    CancelMove(entityId);
                    return false;
                }
            foreach (var m in map.Monsters.Values)
                if (m.InstanceId != entityId && m.X == res.TargetX && m.Y == res.TargetY)
                {
                    CancelMove(entityId);
                    return false;
                }
        }

        // 权威坐标切到目标格
        if (map != null)
        {
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

        res.Confirmed = true;
        return true;
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
        _moveReservations.Remove(entityId);
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
