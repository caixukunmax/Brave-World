using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    public partial class MapEditor : Node2D
    {
        // ============ 摆件拖拽系统 ============

        private void OnDecorationDragRequested(MapDecoration dec)
        {
            if (!IsEditing || CurrentTool != EditorTool.PlaceDecoration || _editGridManager == null)
                return;
            if (_dragMode != DragMode.None)
                return;

            StartMoveDrag(dec);
        }

        private void StartPaletteDrag(int decorationType)
        {
            if (_dragMode != DragMode.None || _editGridManager == null)
                return;

            _dragMode = DragMode.FromPalette;
            _dragDecorationType = decorationType;
            _dragSourceGridPos = new Vector2I(-1, -1);

            CreateDragGhost(decorationType);
            UpdateDragGhostPosition();
            GD.Print($"[MapEditor] 开始从面板拖拽装饰 type={decorationType}");
        }

        private void StartMoveDrag(MapDecoration dec)
        {
            if (_dragMode != DragMode.None || _editGridManager == null)
                return;

            _dragMode = DragMode.MovePlaced;
            _dragDecorationType = dec.BuildCfgId;
            _dragSourceGridPos = dec.GridPos;

            CreateDragGhost(dec.BuildCfgId);
            UpdateDragGhostPosition();
            GD.Print($"[MapEditor] 开始移动已放置装饰 pos={dec.GridPos}");
        }

        private void CreateDragGhost(int decorationTypeId)
        {
            if (_editorPanel == null) return;

            var cfg = DecorationConfigUtil.Get(decorationTypeId);
            var (sizeX, sizeY) = (Mathf.Max(1, cfg.SizeX), Mathf.Max(1, cfg.SizeY));
            var ghost = new Control();
            ghost.CustomMinimumSize = new Vector2(48 * sizeX, 48 * sizeY);
            ghost.Size = new Vector2(48 * sizeX, 48 * sizeY);
            ghost.MouseFilter = Control.MouseFilterEnum.Ignore;
            ghost.ZIndex = 200;
            ghost.ZAsRelative = false;

            var bg = new ColorRect();
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(cfg.Color.R, cfg.Color.G, cfg.Color.B, 0.7f);
            ghost.AddChild(bg);

            // 绘制 footprint 内部网格线，直观显示多格占地
            if (sizeX > 1 || sizeY > 1)
            {
                var gridLines = new Control();
                gridLines.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                gridLines.MouseFilter = Control.MouseFilterEnum.Ignore;
                gridLines.Draw += () => DrawGhostGridLines(gridLines, sizeX, sizeY);
                ghost.AddChild(gridLines);
            }

            var label = new Label();
            label.SetAnchorsPreset(Control.LayoutPreset.Center);
            label.Text = cfg.DisplayName;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 10);
            ghost.AddChild(label);

            var canvasLayer = _editorPanel.GetParent() as CanvasLayer;
            canvasLayer?.AddChild(ghost);
            _dragGhost = ghost;
            UpdateDragGhostPosition();
        }

        private void DrawGhostGridLines(Control control, int sizeX, int sizeY)
        {
            var size = control.Size;
            var cellW = size.X / sizeX;
            var cellH = size.Y / sizeY;
            var lineColor = new Color(1, 1, 1, 0.5f);
            for (int x = 1; x < sizeX; x++)
                control.DrawLine(new Vector2(x * cellW, 0), new Vector2(x * cellW, size.Y), lineColor, 1f);
            for (int y = 1; y < sizeY; y++)
                control.DrawLine(new Vector2(0, y * cellH), new Vector2(size.X, y * cellH), lineColor, 1f);
        }

        private void UpdateDragGhostPosition()
        {
            if (_dragGhost == null) return;
            var mousePos = GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            _dragGhost.Position = mousePos - _dragGhost.Size / 2.0f;
        }

        private void EndDrag(bool cancel)
        {
            if (_dragMode == DragMode.None)
                return;

            GD.Print($"[MapEditor.EndDrag] cancel={cancel} mode={_dragMode}");
            if (!cancel && _editGridManager != null)
            {
                var mouseLocalPos = _editGridManager.ToLocal(GetGlobalMousePosition());
                var gridPos = _editGridManager.WorldToGrid(mouseLocalPos);
                GD.Print($"[MapEditor.EndDrag] drop gridPos={gridPos}");
                TryDropAt(gridPos);
            }

            if (_dragGhost != null && IsInstanceValid(_dragGhost))
            {
                _dragGhost.QueueFree();
                _dragGhost = null;
            }

            _dragMode = DragMode.None;
            _dragDecorationType = 0;
            _dragSourceGridPos = new Vector2I(-1, -1);
        }

        private void TryDropAt(Vector2I gridPos)
        {
            GD.Print($"[MapEditor.TryDropAt] gridPos={gridPos} mode={_dragMode} type={_dragDecorationType}");
            if (_editGridManager == null)
            {
                GD.Print("[MapEditor.TryDropAt] _editGridManager is null");
                return;
            }

            var (sizeX, sizeY) = GetDecorationSize(_dragDecorationType);
            var footprint = GetFootprintCells(gridPos, sizeX, sizeY);

            // 检查 footprint 是否全部在地图范围内
            foreach (var pos in footprint)
            {
                if (!_editGridManager.IsInBounds(pos))
                {
                    GD.Print($"[MapEditor.Drop] 目标 footprint 包含越界格子 {pos}，取消放置");
                    ShowToast("目标位置超出地图范围", Colors.Yellow);
                    return;
                }
            }

            // 检查 footprint 内是否有其他装饰（移动时排除自身原 footprint）
            var occupiedCells = GetOccupiedFootprintCells(_dragSourceGridPos, sizeX, sizeY);
            foreach (var pos in footprint)
            {
                if (occupiedCells.Contains(pos))
                    continue;
                var cell = _editGridManager.GetCell(pos);
                if (cell != null && cell.DecorationType != 0)
                {
                    GD.Print($"[MapEditor.Drop] 目标 footprint 内格子 {pos} 已有装饰，取消放置");
                    ShowToast("目标位置已有装饰", Colors.Yellow);
                    return;
                }
            }

            // 检查多格建筑占地是否与其他建筑占地重叠（锚点不重合时也可能重叠）
            var excludeAnchor = _dragMode == DragMode.MovePlaced ? (Vector2I?)_dragSourceGridPos : null;
            if (IsFootprintOverlapping(gridPos, sizeX, sizeY, excludeAnchor))
            {
                GD.Print($"[MapEditor.Drop] 目标 footprint 与其他建筑占地重叠，取消放置");
                ShowToast("目标位置与其他建筑占地重叠", Colors.Yellow);
                return;
            }

            DecorationEditCommand? cmd = null;

            if (_dragMode == DragMode.FromPalette)
            {
                cmd = CreateDragDecorationCommand((gridPos, 0, _dragDecorationType));
                _editGridManager.GetCell(gridPos).DecorationType = _dragDecorationType;
                GD.Print($"[MapEditor.Drop] 从面板放置装饰 type={_dragDecorationType} pos={gridPos} size={sizeX}x{sizeY}");
            }
            else if (_dragMode == DragMode.MovePlaced)
            {
                if (gridPos == _dragSourceGridPos)
                {
                    GD.Print("[MapEditor.Drop] 移动到原位置，无需修改");
                    return;
                }

                var sourceCell = _editGridManager.GetCell(_dragSourceGridPos);
                if (sourceCell == null)
                    return;
                var targetCell = _editGridManager.GetCell(gridPos);
                if (targetCell == null)
                    return;

                cmd = CreateDragDecorationCommand(
                    (_dragSourceGridPos, sourceCell.DecorationType, 0),
                    (gridPos, targetCell.DecorationType, _dragDecorationType)
                );
                sourceCell.DecorationType = 0;
                targetCell.DecorationType = _dragDecorationType;
                GD.Print($"[MapEditor.Drop] 移动装饰 {_dragSourceGridPos} -> {gridPos} size={sizeX}x{sizeY}");
            }

            if (cmd != null && cmd.Changes.Count > 0)
            {
                _undoStack.Add(cmd);
                if (_undoStack.Count > MaxUndoSteps)
                    _undoStack.RemoveAt(0);
                _redoStack.Clear();
            }

            _editGridManager.NotifyTerrainChanged();
            _editGridManager.SyncDecorations();
        }

        /// <summary>获取建筑 footprint 包含的所有格子（锚点为左上角）</summary>
        private List<Vector2I> GetFootprintCells(Vector2I anchor, int sizeX, int sizeY)
        {
            var cells = new List<Vector2I>();
            sizeX = Mathf.Max(1, sizeX);
            sizeY = Mathf.Max(1, sizeY);
            for (int dy = 0; dy < sizeY; dy++)
                for (int dx = 0; dx < sizeX; dx++)
                    cells.Add(new Vector2I(anchor.X + dx, anchor.Y + dy));
            return cells;
        }

        /// <summary>获取指定锚点建筑的占地格子集合（用于移动时排除自身）</summary>
        private HashSet<Vector2I> GetOccupiedFootprintCells(Vector2I anchor, int sizeX, int sizeY)
        {
            var set = new HashSet<Vector2I>();
            if (anchor.X < 0 || anchor.Y < 0)
                return set;
            foreach (var pos in GetFootprintCells(anchor, sizeX, sizeY))
                set.Add(pos);
            return set;
        }

        /// <summary>读取装饰配置中的占地大小</summary>
        private (int sizeX, int sizeY) GetDecorationSize(int decorationType)
        {
            var cfg = DecorationConfigUtil.Get(decorationType);
            return (Mathf.Max(1, cfg.SizeX), Mathf.Max(1, cfg.SizeY));
        }

        /// <summary>
        /// 检查目标 footprint 是否与任意已有建筑占地重叠。
        /// 由于多格建筑只在锚点格记录 DecorationType，仅靠 DecorationType 检查会漏掉
        /// 锚点不相交但 footprint 相交的情况（如 2x2 房舍错开 1 格）。
        /// </summary>
        private bool IsFootprintOverlapping(Vector2I targetAnchor, int sizeX, int sizeY, Vector2I? excludeAnchor = null)
        {
            if (_editGridManager == null) return false;

            var targetFootprint = GetOccupiedFootprintCells(targetAnchor, sizeX, sizeY);
            foreach (var cell in _editGridManager.GridData.Values)
            {
                if (cell.DecorationType == 0) continue;
                var anchor = cell.Pos;
                if (excludeAnchor.HasValue && anchor == excludeAnchor.Value) continue;

                var (otherSizeX, otherSizeY) = GetDecorationSize(cell.DecorationType);
                var otherFootprint = GetOccupiedFootprintCells(anchor, otherSizeX, otherSizeY);
                if (otherFootprint.Count == 0) continue;

                foreach (var pos in targetFootprint)
                {
                    if (otherFootprint.Contains(pos))
                        return true;
                }
            }
            return false;
        }

        private bool IsMouseOverEditorPanel()
        {
            if (_editorPanel == null || !_editorPanel.Visible)
                return false;
            var mousePos = GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            return _editorPanel.GetGlobalRect().HasPoint(mousePos);
        }

        private void DeleteSelectedDecorations()
        {
            DeleteHoveredDecoration();
        }

        private void DeleteHoveredDecoration()
        {
            if (_editGridManager == null || _hoveredGridPos == new Vector2I(-1, -1))
                return;
            if (!_editGridManager.IsInBounds(_hoveredGridPos))
                return;

            // 通过装饰管理器找到悬停位置所属建筑（支持多格 footprint）
            var decMgr = _editDecorationManager;
            if (decMgr == null) return;

            var dec = decMgr.GetDecorationAt(_hoveredGridPos);
            if (dec == null)
                return;

            var anchor = new Vector2I(dec.GridX, dec.GridY);
            var cell = _editGridManager.GetCell(anchor);
            if (cell == null || cell.DecorationType == 0)
                return;

            var cmd = CreateDragDecorationCommand((anchor, cell.DecorationType, 0));
            _undoStack.Add(cmd);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();

            cell.DecorationType = 0;
            _editGridManager.NotifyTerrainChanged();
            _editGridManager.SyncDecorations();
            GD.Print($"[MapEditor.DeleteHoveredDecoration] anchor={anchor} type={cell.DecorationType}");
        }

        private void UpdateSelectionLabel()
        {
            // 已选中计数已从右侧边栏移除，此方法保留以避免大面积改动调用点
        }

        /// <summary>
        /// 创建放置/移动装饰撤销命令。
        /// </summary>
        private DecorationEditCommand CreateDragDecorationCommand(params (Vector2I Pos, int OldType, int NewType)[] changes)
        {
            var cmd = new DecorationEditCommand();
            foreach (var change in changes)
            {
                if (change.OldType != change.NewType)
                    cmd.Changes.Add(change);
            }
            return cmd;
        }
    }
}
