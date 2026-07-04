using System.Collections.Generic;
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
        private int _gridSize = 111;

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
        /// 从稀疏格子数据生成装饰摆件。
        /// </summary>
        public void SpawnDecorations(Dictionary<Vector2I, GridCell> gridData)
        {
            ClearDecorations();
            if (gridData == null) return;

            foreach (var cell in gridData.Values)
            {
                if (cell.DecorationType == 0) continue;
                SpawnDecoration(cell.Pos, cell.DecorationType);
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

            foreach (var tile in tiles)
            {
                if (tile.DecorationType == 0) continue;
                SpawnDecoration(new Vector2I(tile.X, tile.Y), tile.DecorationType);
            }
        }

        private void SpawnDecoration(Vector2I pos, int decorationTypeId)
        {
            if (_decorations.ContainsKey(pos))
                return;

            var dec = new MapDecoration();
            dec.Setup(decorationTypeId, pos.X, pos.Y, _gridSize);
            AddChild(dec);
            _decorations[pos] = dec;
        }

        public void ClearDecorations()
        {
            foreach (var dec in _decorations.Values)
            {
                if (IsInstanceValid(dec))
                    dec.QueueFree();
            }
            _decorations.Clear();
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
            return _decorations.ContainsKey(pos);
        }

        public MapDecoration? GetDecorationAt(Vector2I pos)
        {
            return _decorations.TryGetValue(pos, out var dec) && IsInstanceValid(dec) ? dec : null;
        }
    }
}
