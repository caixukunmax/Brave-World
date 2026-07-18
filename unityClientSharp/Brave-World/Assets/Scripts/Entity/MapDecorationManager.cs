using System.Collections.Generic;
using System.Linq;
using UnityClientSharp.Map.Core;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 地图装饰摆件管理器。
    /// 移植自 clinetcsharp/Scripts/MapDecorationManager.cs：
    /// 根据 GridData 的装饰字段创建/销毁 MapDecoration 实例（多格建筑只生成一个实例，锚点为占地左上角）。
    /// 裁剪：网络 TileInfo 生成入口（随网络层阶段接入）、编辑模式 SpawnEditable。
    /// </summary>
    public class MapDecorationManager : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, MapDecoration> _decorations = new();
        private readonly Dictionary<int, int> _uidCounters = new();
        private int _gridSize = 111;

        /// <summary>生成的摆件是否可编辑（地图编辑器阶段使用；游戏运行时出生点不生成）</summary>
        public bool SpawnEditable;

        private void Awake()
        {
            Walkability.Decorations = this;
        }

        private void OnDestroy()
        {
            if (Walkability.Decorations == this)
                Walkability.Decorations = null;
        }

        public int GridSize
        {
            get => _gridSize;
            set => _gridSize = value;
        }

        /// <summary>从稀疏格子数据生成装饰摆件。</summary>
        public void SpawnDecorations(Dictionary<Vector2Int, GridCell> gridData)
        {
            ClearDecorations();
            if (gridData == null) return;

            // 先按坐标排序，确保锚点优先处理
            var orderedCells = gridData.Values
                .Where(c => c.DecorationType != 0)
                .OrderBy(c => c.Pos.y)
                .ThenBy(c => c.Pos.x)
                .ToList();

            var occupiedByBuilding = new HashSet<Vector2Int>();
            foreach (var cell in orderedCells)
            {
                if (occupiedByBuilding.Contains(cell.Pos))
                    continue;

                var dec = SpawnDecoration(cell.Pos, cell.DecorationType);
                if (dec != null)
                {
                    // 标记该建筑占地范围，避免重复生成
                    for (int dx = 0; dx < dec.SizeX; dx++)
                        for (int dy = 0; dy < dec.SizeY; dy++)
                            occupiedByBuilding.Add(new Vector2Int(cell.Pos.x + dx, cell.Pos.y + dy));
                }
            }

            Debug.Log($"[MapDecorationManager] 生成 {gridData.Count} 个格子中的 {_decorations.Count} 个装饰摆件");
        }

        public MapDecoration SpawnDecoration(Vector2Int pos, int profileId, int sizeX = 0, int sizeY = 0)
        {
            if (_decorations.ContainsKey(pos))
                return _decorations[pos];

            // 防御：玩家/怪物/NPC 的 Profile ID 不能用作建筑装饰
            if (profileId >= 1 && profileId <= 3)
            {
                Debug.LogError($"[MapDecorationManager] 拒绝使用玩家/怪物/NPC Profile {profileId} 作为建筑装饰 at {pos}");
                return null;
            }

            // 兼容旧 decoration type（1=房舍，2=商店），转换为 build_cfg_id。
            // 注意：与 Godot 一致，上面的 1-3 拒绝分支会先拦截原始 1/2，此映射实际是保留的死代码；
            // 旧地图的 1/10/11/12 已在 JSON 加载期由 GridCell.MigrateOldDecorationType 转换。
            if (profileId == BuildingType.House)
                profileId = BuildingType.GetConfigBaseId(BuildingType.House) + 1;
            else if (profileId == BuildingType.Shop)
                profileId = BuildingType.GetConfigBaseId(BuildingType.Shop);

            // 出生点仅在编辑器中生成，游戏运行时完全隐身
            if (!SpawnEditable && profileId == BuildingType.GetConfigBaseId(BuildingType.SpawnPoint))
                return null;

            int buildingType = BuildingType.GetTypeFromConfigId(profileId);
            if (!BuildingType.IsValid(buildingType))
                buildingType = BuildingType.House;

            // 从 EntityProfile 读取占地大小
            if (sizeX <= 0 || sizeY <= 0)
            {
                var app = EntityProfileManager.GetProfile(profileId)?.GetData<AppearanceData>("appearance");
                sizeX = app?.SizeX ?? 1;
                sizeY = app?.SizeY ?? 1;
            }
            sizeX = Mathf.Max(1, sizeX);
            sizeY = Mathf.Max(1, sizeY);

            if (!_uidCounters.TryGetValue(buildingType, out int seq))
                seq = 0;
            int buildingUid = BuildingType.GetUidBaseId(buildingType) + seq;
            _uidCounters[buildingType] = seq + 1;

            var go = new GameObject($"MapDecoration_{pos.x}_{pos.y}");
            go.transform.SetParent(transform, false);
            var dec = go.AddComponent<MapDecoration>();
            dec.Setup(profileId, pos.x, pos.y, _gridSize, buildingUid, sizeX, sizeY);
            _decorations[pos] = dec;
            return dec;
        }

        public bool RemoveDecorationAt(Vector2Int pos)
        {
            var dec = GetDecorationAt(pos);
            if (dec == null)
                return false;

            var anchor = new Vector2Int(dec.GridX, dec.GridY);
            _decorations.Remove(anchor);
            if (dec != null)
                Destroy(dec.gameObject);
            return true;
        }

        public void ClearDecorations()
        {
            foreach (var dec in _decorations.Values)
            {
                if (dec != null)
                    Destroy(dec.gameObject);
            }
            _decorations.Clear();
            _uidCounters.Clear();
        }

        public bool HasDecorationAt(Vector2Int pos) => GetDecorationAt(pos) != null;

        /// <summary>按 footprint 命中查询装饰（pos 落在任一建筑占地内即返回）。</summary>
        public MapDecoration GetDecorationAt(Vector2Int pos)
        {
            foreach (var dec in _decorations.Values)
            {
                if (dec == null)
                    continue;
                int minX = dec.GridX;
                int minY = dec.GridY;
                int maxX = minX + dec.SizeX - 1;
                int maxY = minY + dec.SizeY - 1;
                if (pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY)
                    return dec;
            }
            return null;
        }

        /// <summary>该格子是否被阻挡移动的建筑 footprint 覆盖（供 GridManager.IsWalkable 使用）。</summary>
        public bool IsBlockedByDecoration(Vector2Int pos)
        {
            var dec = GetDecorationAt(pos);
            return dec != null && dec.BlockMovement;
        }
    }
}
