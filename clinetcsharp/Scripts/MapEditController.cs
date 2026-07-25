using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图编辑共享核心：不依赖任何运行时游戏节点，只操作 GridManager.GridData。
    /// 运行时 MapEditor 与编辑器插件（addons/map_editor_editor）共用同一套数据编辑逻辑，
    /// 从而保证“运行游戏内热编辑”与“不运行游戏用插件编辑”行为完全一致。
    ///
    /// 调用方负责：输入坐标换算、相机、把装饰渲染出来（运行时生成 MapDecoration 节点，
    /// 插件用 MapEditDecorationOverlay 画占地块）。控制器只管数据 + 撤销/重做 + 选择集合。
    /// </summary>
    public class MapEditController
    {
        public enum MapEditTool
        {
            PaintTerrain,   // 选格 + 调色板改 DecorationType（地形类）
            PlaceDecoration // 摆放/移动/删除装饰（footprint 感知）
        }

        public GridManager Grid;
        public Dictionary<Vector2I, bool> SelectedCells = new();
        public bool IsSelecting;
        public Vector2I SelectionStart;
        public Vector2I SelectionEnd;
        public bool Dirty;

        private readonly List<EditCommand> _undoStack = new();
        private readonly List<EditCommand> _redoStack = new();
        public const int MaxUndoSteps = 20;

        /// <summary>数据变更后触发，调用方据此刷新渲染（SyncDecorations / 重画 overlay 等）。</summary>
        public event System.Action OnVisualChanged;

        private Dictionary<Vector2I, bool> _selectionBeforeDrag = new();
        private bool _selectionStartedWithCtrl;

        public MapEditController(GridManager grid)
        {
            Grid = grid;
        }

        // ============ 选择系统 ============

        public void BeginSelection(Vector2I gridPos, bool additive)
        {
            if (Grid == null) return;
            SelectionStart = gridPos;
            SelectionEnd = gridPos;
            IsSelecting = true;
            _selectionStartedWithCtrl = additive;
            _selectionBeforeDrag = new Dictionary<Vector2I, bool>(SelectedCells);
        }

        public void UpdateSelection(Vector2I gridPos)
        {
            SelectionEnd = gridPos;
        }

        public void EndSelection()
        {
            if (!IsSelecting) return;
            IsSelecting = false;

            if (SelectionStart == SelectionEnd)
            {
                if (!_selectionStartedWithCtrl) SelectedCells.Clear();
                SelectedCells[SelectionStart] = true;
            }
            else
            {
                if (!_selectionStartedWithCtrl) SelectedCells.Clear();
                int minX = Mathf.Min(SelectionStart.X, SelectionEnd.X);
                int maxX = Mathf.Max(SelectionStart.X, SelectionEnd.X);
                int minY = Mathf.Min(SelectionStart.Y, SelectionEnd.Y);
                int maxY = Mathf.Max(SelectionStart.Y, SelectionEnd.Y);
                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        SelectedCells[new Vector2I(x, y)] = true;
            }

            if (!SelectionEquals(_selectionBeforeDrag, SelectedCells))
            {
                PushUndo(new SelectionEditCommand
                {
                    Controller = this,
                    OldSelection = _selectionBeforeDrag,
                    NewSelection = new Dictionary<Vector2I, bool>(SelectedCells)
                });
            }
            OnVisualChanged?.Invoke();
        }

        public void SelectAll()
        {
            if (Grid == null) return;
            SelectedCells.Clear();
            foreach (var pos in Grid.GridData.Keys) SelectedCells[pos] = true;
            OnVisualChanged?.Invoke();
        }

        public void ClearSelection()
        {
            SelectedCells.Clear();
            OnVisualChanged?.Invoke();
        }

        public void InvertSelection()
        {
            if (Grid == null) return;
            var nv = new Dictionary<Vector2I, bool>();
            foreach (var pos in Grid.GridData.Keys)
                if (!SelectedCells.ContainsKey(pos)) nv[pos] = true;
            SelectedCells = nv;
            OnVisualChanged?.Invoke();
        }

        public bool HasOutOfBoundsSelection()
        {
            if (Grid == null || SelectedCells.Count == 0) return false;
            foreach (var pos in SelectedCells.Keys)
                if (!Grid.IsInBounds(pos)) return true;
            return false;
        }

        public Rect2I GetSelectionBounds()
        {
            if (SelectedCells.Count == 0) return new Rect2I();
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var pos in SelectedCells.Keys)
            {
                if (pos.X < minX) minX = pos.X;
                if (pos.X > maxX) maxX = pos.X;
                if (pos.Y < minY) minY = pos.Y;
                if (pos.Y > maxY) maxY = pos.Y;
            }
            return new Rect2I(new Vector2I(minX, minY), new Vector2I(maxX - minX + 1, maxY - minY + 1));
        }

        // ============ 应用装饰（刷地形/放建筑）到选区 ============

        public bool IsValidTerrainDecorationId(int id)
        {
            if (id == 0) return true;
            if (id >= 1 && id <= 3) return false;
            var cfg = DecorationConfigUtil.Get(id);
            return cfg != null && cfg.Id == id && cfg.Category == "Terrain";
        }

        /// <summary>把 decorationId 应用到当前选区（刷地形/放建筑都走这里，仅 palette 过滤不同）。</summary>
        public void ApplyDecorationTypeToSelection(int decorationId)
        {
            if (Grid == null || SelectedCells.Count == 0) return;

            var cmd = new DecorationEditCommand();
            foreach (var pos in SelectedCells.Keys)
            {
                if (!Grid.IsInBounds(pos)) continue;
                var cell = Grid.GetCell(pos);
                if (cell == null) continue;
                if (cell.DecorationType == decorationId) continue;
                cmd.Changes.Add((pos, cell.DecorationType, decorationId));
            }
            if (cmd.Changes.Count == 0) return;

            foreach (var (pos, _, _) in cmd.Changes)
            {
                var cell = Grid.GetCell(pos);
                if (cell != null) cell.DecorationType = decorationId;
            }
            PushUndo(cmd);
            Grid.NotifyTerrainChanged();
            OnVisualChanged?.Invoke();
        }

        // ============ 摆放 / 移动 / 删除装饰（footprint 感知）============

        public bool TryPlaceDecoration(Vector2I gridPos, int decorationType, Vector2I? sourceAnchor = null)
        {
            if (Grid == null) return false;
            var (sizeX, sizeY) = GetDecorationSize(decorationType);
            var footprint = GetFootprintCells(gridPos, sizeX, sizeY);
            foreach (var pos in footprint)
                if (!Grid.IsInBounds(pos)) return false;

            // 排除正在移动自身来源锚点
            var excludeAnchor = sourceAnchor;
            if (IsFootprintOverlapping(gridPos, sizeX, sizeY, excludeAnchor)) return false;

            var cmd = new DecorationEditCommand();
            if (sourceAnchor.HasValue && sourceAnchor.Value.X >= 0)
            {
                if (gridPos == sourceAnchor.Value) return false;
                var src = Grid.GetCell(sourceAnchor.Value);
                var tgt = Grid.GetCell(gridPos);
                if (src == null || tgt == null) return false;
                cmd.Changes.Add((sourceAnchor.Value, src.DecorationType, 0));
                cmd.Changes.Add((gridPos, tgt.DecorationType, decorationType));
                src.DecorationType = 0;
                tgt.DecorationType = decorationType;
            }
            else
            {
                var cell = Grid.GetCell(gridPos);
                if (cell == null) return false;
                cmd.Changes.Add((gridPos, cell.DecorationType, decorationType));
                cell.DecorationType = decorationType;
            }

            if (cmd.Changes.Count == 0) return false;
            PushUndo(cmd);
            Grid.NotifyTerrainChanged();
            OnVisualChanged?.Invoke();
            return true;
        }

        public bool DeleteDecorationAt(Vector2I gridPos)
        {
            if (Grid == null) return false;
            var anchor = FindAnchorAt(gridPos);
            if (anchor == null) return false;
            var cell = Grid.GetCell(anchor.Value);
            if (cell == null || cell.DecorationType == 0) return false;
            var cmd = new DecorationEditCommand();
            cmd.Changes.Add((anchor.Value, cell.DecorationType, 0));
            cell.DecorationType = 0;
            PushUndo(cmd);
            Grid.NotifyTerrainChanged();
            OnVisualChanged?.Invoke();
            return true;
        }

        /// <summary>找到包含 gridPos 的装饰锚点（footprint 反查）。</summary>
        public Vector2I? FindAnchorAt(Vector2I gridPos)
        {
            if (Grid == null) return null;
            foreach (var cell in Grid.GridData.Values)
            {
                if (cell.DecorationType == 0) continue;
                var (sx, sy) = GetDecorationSize(cell.DecorationType);
                if (GetOccupiedFootprintCells(cell.Pos, sx, sy).Contains(gridPos))
                    return cell.Pos;
            }
            return null;
        }

        // ============ 地图管理 ============

        public bool LoadMap(string name)
        {
            if (Grid == null) return false;
            var data = MapDataManager.LoadMapFromJson(name, out var bounds, out _, out _);
            if (data == null) return false;
            Grid.CurrentMapName = name;
            Grid.ApplyLoadedGridData(data, bounds);
            SelectedCells.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            Dirty = false;
            OnVisualChanged?.Invoke();
            return true;
        }

        public Error SaveMap()
        {
            if (Grid == null) return Error.Failed;
            Dirty = false;
            return Grid.SaveCurrentMap();
        }

        /// <summary>
        /// 由命令类在声明类内部触发视觉刷新事件（C# 事件不允许外部直接 Invoke）。
        /// </summary>
        public void TriggerVisualChanged() => OnVisualChanged?.Invoke();

        public static List<string> GetMapList() => MapDataManager.GetMapList();
        public static bool MapExists(string n) => MapDataManager.MapExists(n);
        public static bool CreateNewMap(string n, int w, int h) => MapDataManager.CreateNewMap(n, w, h) == Error.Ok;
        public static bool DeleteMap(string n) => MapDataManager.DeleteMap(n) == Error.Ok;
        public static bool RenameMap(string o, string n) => MapDataManager.RenameMap(o, n) == Error.Ok;

        public bool ExtendMap(List<Vector2I> cellsToAdd)
        {
            if (Grid == null) return false;
            var cmd = new ExtendMapCommand { OldGridData = DeepCopyGridData(Grid.GridData) };
            var err = Grid.ExtendMap(cellsToAdd);
            if (err != Error.Ok) return false;
            cmd.NewGridData = DeepCopyGridData(Grid.GridData);
            PushUndo(cmd);
            SelectedCells.Clear();
            OnVisualChanged?.Invoke();
            return true;
        }

        // ============ 撤销 / 重做 ============

        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            var cmd = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            cmd.Undo(Grid);
            _redoStack.Add(cmd);
            OnVisualChanged?.Invoke();
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            var cmd = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            cmd.Redo(Grid);
            _undoStack.Add(cmd);
            OnVisualChanged?.Invoke();
        }

        private void PushUndo(EditCommand cmd)
        {
            _undoStack.Add(cmd);
            if (_undoStack.Count > MaxUndoSteps) _undoStack.RemoveAt(0);
            _redoStack.Clear();
            Dirty = true;
        }

        // ============ footprint 辅助 ============

        public (int, int) GetDecorationSize(int t)
        {
            var cfg = DecorationConfigUtil.Get(t);
            return (Mathf.Max(1, cfg.SizeX), Mathf.Max(1, cfg.SizeY));
        }

        public List<Vector2I> GetFootprintCells(Vector2I anchor, int sx, int sy)
        {
            var cells = new List<Vector2I>();
            sx = Mathf.Max(1, sx);
            sy = Mathf.Max(1, sy);
            for (int dy = 0; dy < sy; dy++)
                for (int dx = 0; dx < sx; dx++)
                    cells.Add(new Vector2I(anchor.X + dx, anchor.Y + dy));
            return cells;
        }

        public HashSet<Vector2I> GetOccupiedFootprintCells(Vector2I anchor, int sx, int sy)
        {
            var set = new HashSet<Vector2I>();
            if (anchor.X < 0 || anchor.Y < 0) return set;
            foreach (var p in GetFootprintCells(anchor, sx, sy)) set.Add(p);
            return set;
        }

        public bool IsFootprintOverlapping(Vector2I target, int sx, int sy, Vector2I? exclude = null)
        {
            var targetFp = GetOccupiedFootprintCells(target, sx, sy);
            foreach (var cell in Grid.GridData.Values)
            {
                if (cell.DecorationType == 0) continue;
                var anchor = cell.Pos;
                if (exclude.HasValue && anchor == exclude.Value) continue;
                var (ox, oy) = GetDecorationSize(cell.DecorationType);
                var otherFp = GetOccupiedFootprintCells(anchor, ox, oy);
                if (otherFp.Count == 0) continue;
                foreach (var p in targetFp)
                    if (otherFp.Contains(p)) return true;
            }
            return false;
        }

        private Dictionary<Vector2I, GridCell> DeepCopyGridData(Dictionary<Vector2I, GridCell> src)
        {
            var copy = new Dictionary<Vector2I, GridCell>();
            foreach (var kvp in src)
            {
                var c = kvp.Value;
                var n = new GridCell(c.Pos.X, c.Pos.Y);
                n.Uid = c.Uid;
                n.TerrainType = c.TerrainType;
                n.DecorationType = c.DecorationType;
                n.Height = c.Height;
                n.CustomData = c.CustomData;
                n.TerrainConfig = c.TerrainConfig;
                copy[kvp.Key] = n;
            }
            return copy;
        }

        private bool SelectionEquals(Dictionary<Vector2I, bool> a, Dictionary<Vector2I, bool> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kvp in a)
                if (!b.ContainsKey(kvp.Key)) return false;
            return true;
        }
    }
}
