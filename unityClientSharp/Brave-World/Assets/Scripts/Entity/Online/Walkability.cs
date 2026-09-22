using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 行走判定门面 — 移植自 Godot GridManager.IsWalkable 的完整判定链：
    /// 格子存在 → 地形 Walkable → 装饰建筑 footprint → 未开宝箱 → 怪物（当前 + 预约 footprint）→ NPC footprint。
    /// 各管理器在 Awake 自注册、OnDestroy 反注册；调用方一律用本门面而非各自查询。
    /// </summary>
    public static class Walkability
    {
        public static GridManager Grid;
        public static MapDecorationManager Decorations;
        public static ChestManager Chests;
        public static MonsterManager Monsters;
        public static NpcManager Npcs;

        public static bool IsWalkable(Vector2Int pos)
        {
            if (Grid == null || !Grid.GridData.TryGetValue(pos, out var cell))
                return false;
            if (Decorations != null && Decorations.IsBlockedByDecoration(pos))
                return false;
            if (Chests != null && Chests.IsBlockedByChest(pos))
                return false;
            if (Monsters != null && Monsters.IsBlockedByMonster(pos))
                return false;
            if (Npcs != null && Npcs.IsBlockedByNpc(pos))
                return false;
            return (cell.TerrainConfig ?? TerrainConfigUtil.Get(cell.TerrainType))?.Walkable ?? true;
        }
    }
}
