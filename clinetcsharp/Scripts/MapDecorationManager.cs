using System.Collections.Generic;
using System.Linq;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图装饰摆件管理器。
    /// 负责根据 GridData 或网络下发的装饰数据创建/销毁 MapDecoration 实例。
    /// </summary>
    public partial class MapDecorationManager : Node
    {
        private readonly Dictionary<Vector2I, MapDecoration> _decorations = new();
        private readonly Dictionary<int, int> _uidCounters = new();
        private int _gridSize = 111;

        /// <summary>生成的摆件是否可编辑（用于地图编辑器）</summary>
        public bool SpawnEditable { get; set; } = false;

        public int GridSize
        {
            get => _gridSize;
            set
            {
                _gridSize = value;
                foreach (var dec in _decorations.Values)
                    dec.SetGridSize(value);
            }
        }

        /// <summary>
        /// 从稀疏格子数据生成装饰摆件。多格建筑只生成一个实例，锚点为占地左上角。
        /// </summary>
        public void SpawnDecorations(Dictionary<Vector2I, GridCell> gridData)
        {
            ClearDecorations();
            if (gridData == null) return;

            // 先按坐标排序，确保锚点优先处理
            var orderedCells = gridData.Values
                .Where(c => c.DecorationType != 0)
                .OrderBy(c => c.Pos.Y)
                .ThenBy(c => c.Pos.X)
                .ToList();

            var occupiedByBuilding = new HashSet<Vector2I>();
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
                            occupiedByBuilding.Add(new Vector2I(cell.Pos.X + dx, cell.Pos.Y + dy));
                }
            }

            GD.Print($"[MapDecorationManager] 生成 {gridData.Count} 个格子中的 {_decorations.Count} 个装饰摆件");
        }

        /// <summary>
        /// 从网络 TileInfo 列表生成装饰摆件。
        /// </summary>
        public void SpawnDecorationsFromTiles(IEnumerable<Game.TileInfo> tiles)
        {
            ClearDecorations();
            if (tiles == null) return;

            // 按坐标排序，锚点优先
            var orderedTiles = tiles
                .Where(t => t.DecorationType != 0)
                .OrderBy(t => t.Y)
                .ThenBy(t => t.X)
                .ToList();

            var occupiedByBuilding = new HashSet<Vector2I>();
            foreach (var tile in orderedTiles)
            {
                var pos = new Vector2I(tile.X, tile.Y);
                if (occupiedByBuilding.Contains(pos))
                    continue;

                // 阶段 1：服务端 TileInfo 的 size 暂不可靠，从 EntityProfile 读取真实占地
                var dec = SpawnDecoration(pos, tile.DecorationType, 0, 0);
                if (dec != null)
                {
                    for (int dx = 0; dx < dec.SizeX; dx++)
                        for (int dy = 0; dy < dec.SizeY; dy++)
                            occupiedByBuilding.Add(new Vector2I(pos.X + dx, pos.Y + dy));
                }
            }
        }

        public MapDecoration? SpawnDecoration(Vector2I pos, int profileId, int sizeX = 0, int sizeY = 0)
        {
            if (_decorations.ContainsKey(pos))
                return _decorations[pos];

            // 兼容旧 decoration type（1=房舍，2=商店），转换为 build_cfg_id
            if (profileId == BuildingType.House)
                profileId = BuildingType.GetConfigBaseId(BuildingType.House);
            else if (profileId == BuildingType.Shop)
                profileId = BuildingType.GetConfigBaseId(BuildingType.Shop);

            int buildingType = BuildingType.GetTypeFromConfigId(profileId);
            if (!BuildingType.IsValid(buildingType))
                buildingType = BuildingType.House;

            // 从 EntityProfile 读取占地大小
            if (sizeX <= 0 || sizeY <= 0)
            {
                var profile = EntityProfileManager.Instance?.GetProfile(profileId);
                var app = profile?.GetData<AppearanceData>("appearance");
                sizeX = app?.SizeX ?? 1;
                sizeY = app?.SizeY ?? 1;
            }
            sizeX = Mathf.Max(1, sizeX);
            sizeY = Mathf.Max(1, sizeY);

            if (!_uidCounters.TryGetValue(buildingType, out int seq))
                seq = 0;
            int buildingUid = BuildingType.GetUidBaseId(buildingType) + seq;
            _uidCounters[buildingType] = seq + 1;

            var dec = new MapDecoration();
            dec.Setup(profileId, pos.X, pos.Y, _gridSize, buildingUid, sizeX, sizeY);
            dec.IsEditable = SpawnEditable;
            AddChild(dec);
            _decorations[pos] = dec;

            GD.Print($"[MapDecorationManager] Spawned decoration uid={buildingUid} cfg={profileId} size={sizeX}x{sizeY} at {pos}");
            return dec;
        }

        public bool RemoveDecorationAt(Vector2I pos)
        {
            var dec = GetDecorationAt(pos);
            if (dec == null)
                return false;

            var anchor = new Vector2I(dec.GridX, dec.GridY);
            _decorations.Remove(anchor);
            if (IsInstanceValid(dec))
                dec.QueueFree();
            return true;
        }

        public void ClearDecorations()
        {
            foreach (var dec in _decorations.Values)
            {
                if (IsInstanceValid(dec))
                    dec.QueueFree();
            }
            _decorations.Clear();
            _uidCounters.Clear();
        }

        public void SetAllDecorationsVisible(bool visible)
        {
            foreach (var dec in _decorations.Values)
            {
                if (dec is CanvasItem ci && IsInstanceValid(ci))
                    ci.Visible = visible;
            }
        }

        public bool HasDecorationAt(Vector2I pos)
        {
            return GetDecorationAt(pos) != null;
        }

        public MapDecoration? GetDecorationAt(Vector2I pos)
        {
            foreach (var dec in _decorations.Values)
            {
                if (!IsInstanceValid(dec))
                    continue;
                int minX = dec.GridX;
                int minY = dec.GridY;
                int maxX = minX + dec.SizeX - 1;
                int maxY = minY + dec.SizeY - 1;
                if (pos.X >= minX && pos.X <= maxX && pos.Y >= minY && pos.Y <= maxY)
                    return dec;
            }
            return null;
        }
    }
}
