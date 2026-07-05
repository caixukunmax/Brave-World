using System.Collections.Concurrent;

namespace GameServer.Services.Core;

/// <summary>
/// 世界状态只读接口 — 供 Combat/Monster/AI 查询，不暴露可变状态
/// </summary>
public interface IWorldState
{
    /// <summary>查找实体所在地图名和坐标</summary>
    (string? mapName, (int x, int y)? pos) FindEntityPosition(long entityId);

    /// <summary>获取实体名称</summary>
    string GetEntityName(long entityId);

    /// <summary>获取地图上所有玩家</summary>
    ConcurrentDictionary<long, MapPlayerState> GetPlayersOnMap(string mapName);

    /// <summary>获取地图上所有怪物</summary>
    ConcurrentDictionary<long, MapMonsterState> GetMonstersOnMap(string mapName);

    /// <summary>检查格子是否可行走</summary>
    bool IsWalkable(string mapName, int x, int y);

    /// <summary>查找最近的可行走格子</summary>
    (int x, int y)? FindNearestWalkable(string mapName, int x, int y);

    /// <summary>查找最近的、能容纳指定 footprint 的锚点位置</summary>
    (int x, int y)? FindNearestWalkableForFootprint(string mapName, int x, int y, int sizeX, int sizeY,
        int maxRadius = 10, bool requireVacant = true, long excludedEntityId = 0);

    /// <summary>检查格子是否被实体占据</summary>
    bool IsOccupied(string mapName, int x, int y);

    /// <summary>获取所有地图状态（供 tick 使用）</summary>
    ConcurrentDictionary<string, MapState> GetAllMaps();
}
