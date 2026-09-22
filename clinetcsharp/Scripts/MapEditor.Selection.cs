using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    public partial class MapEditor : Node2D
    {
        // ============ 选中系统 ============

        private void StartSelection(InputEventMouseButton @event)
        {
            if (GridManager == null)
                return;

            // 将全局鼠标坐标转换到 GridManager 本地空间再计算网格坐标
            var mouseLocalPos = GridManager.ToLocal(GetGlobalMousePosition());
            var gridPos = GridManager.WorldToGrid(mouseLocalPos);

            // 保存当前选择状态到历史（用于右键撤销）
            SaveSelectionHistory();

            // 记录框选/选择前的选区，用于生成撤销命令
            _selectionBeforeDrag = new Dictionary<Vector2I, bool>(SelectedCells);

            // 记录框选开始时的 Ctrl 状态，整个选择过程保持该语义
            _selectionStartedWithCtrl = @event.CtrlPressed;

            // 开始框选（包括 Ctrl+拖拽多选）
            IsSelecting = true;
            SelectionStart = gridPos;
            SelectionEnd = gridPos;
            QueueRedraw();

            // 消费事件，防止 CameraController 同时开始拖拽
            GetViewport()?.SetInputAsHandled();
        }

        private void UpdateSelection(InputEventMouseMotion @event)
        {
            if (GridManager == null)
                return;

            var mouseLocalPos = GridManager.ToLocal(GetGlobalMousePosition());
            var gridPos = GridManager.WorldToGrid(mouseLocalPos);

            SelectionEnd = gridPos;
            QueueRedraw();
        }

        private void EndSelection()
        {
            if (!IsSelecting)
                return;

            IsSelecting = false;

            // 检查是否是单点点击（不是拖拽）
            if (SelectionStart == SelectionEnd)
            {
                // 单选：清除之前的选择，只选当前格子
                if (!_selectionStartedWithCtrl)
                    SelectedCells.Clear();
                SelectedCells[SelectionStart] = true;
            }
            else
            {
                // 框选：框内的格子加入选择
                if (!_selectionStartedWithCtrl)
                    SelectedCells.Clear();

                var minX = Mathf.Min(SelectionStart.X, SelectionEnd.X);
                var maxX = Mathf.Max(SelectionStart.X, SelectionEnd.X);
                var minY = Mathf.Min(SelectionStart.Y, SelectionEnd.Y);
                var maxY = Mathf.Max(SelectionStart.Y, SelectionEnd.Y);

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        var gridPos = new Vector2I(x, y);
                        SelectedCells[gridPos] = true;
                    }
                }
            }

            // 如果选区发生变化，生成一个撤销命令
            if (!SelectionEquals(_selectionBeforeDrag, SelectedCells))
            {
                var cmd = new SelectionEditCommand(this)
                {
                    OldSelection = _selectionBeforeDrag,
                    NewSelection = new Dictionary<Vector2I, bool>(SelectedCells)
                };
                _undoStack.Add(cmd);
                if (_undoStack.Count > MaxUndoSteps)
                    _undoStack.RemoveAt(0);
                _redoStack.Clear();
                GD.Print($"[MapEditor.EndSelection] 选区变更已加入撤销栈，当前选中 {SelectedCells.Count} 个格子");
            }

            UpdateSelectionLabel();
            QueueRedraw();
        }

        private bool SelectionEquals(Dictionary<Vector2I, bool> a, Dictionary<Vector2I, bool> b)
        {
            if (a.Count != b.Count)
                return false;
            foreach (var kvp in a)
            {
                if (!b.ContainsKey(kvp.Key))
                    return false;
            }
            return true;
        }

        private void SelectAll()
        {
            if (GridManager == null)
                return;

            SelectedCells.Clear();
            foreach (var pos in GridManager.GridData.Keys)
                SelectedCells[pos] = true;

            UpdateSelectionLabel();
            QueueRedraw();
        }

        private void ClearSelection()
        {
            SelectedCells.Clear();
            UpdateSelectionLabel();
            QueueRedraw();
        }

        private void InvertSelection()
        {
            if (GridManager == null)
                return;

            var newSelection = new Dictionary<Vector2I, bool>();
            foreach (var pos in GridManager.GridData.Keys)
            {
                if (!SelectedCells.ContainsKey(pos))
                    newSelection[pos] = true;
            }

            SelectedCells = newSelection;
            UpdateSelectionLabel();
            QueueRedraw();
        }

        // ============ 选择历史系统（右键撤销） ============

        private void SaveSelectionHistory()
        {
            // 保存当前选择状态到历史栈
            var historyCopy = new Dictionary<Vector2I, bool>();
            foreach (var kvp in SelectedCells)
                historyCopy[kvp.Key] = kvp.Value;

            _selectionHistory.Add(historyCopy);
            if (_selectionHistory.Count > MaxSelectionHistory)
                _selectionHistory.RemoveAt(0);
        }

        private void UndoSelection()
        {
            // 右键撤销上一步选择
            if (_selectionHistory.Count == 0)
            {
                GD.Print("[MapEditor] 没有选择历史可撤销");
                return;
            }

            // 恢复上一个选择状态
            SelectedCells = _selectionHistory[_selectionHistory.Count - 1];
            _selectionHistory.RemoveAt(_selectionHistory.Count - 1);
            IsSelecting = false;
            UpdateSelectionLabel();
            QueueRedraw();
            GD.Print("[MapEditor] 已撤销上一步选择，当前选中: " + SelectedCells.Count + " 个格子");
        }

        // ============ 地图扩展系统 ============

        /// <summary>
        /// 判断当前选区是否包含地图外部的格子。
        /// </summary>
        private bool HasOutOfBoundsSelection()
        {
            if (GridManager == null || SelectedCells.Count == 0)
                return false;

            foreach (var pos in SelectedCells.Keys)
            {
                if (!GridManager.IsInBounds(pos))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取当前框选/选中的逻辑坐标矩形范围。
        /// </summary>
        private Rect2I GetSelectionBounds()
        {
            if (SelectedCells.Count == 0)
                return new Rect2I();

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var pos in SelectedCells.Keys)
            {
                if (pos.X < minX) minX = pos.X;
                if (pos.X > maxX) maxX = pos.X;
                if (pos.Y < minY) minY = pos.Y;
                if (pos.Y > maxY) maxY = pos.Y;
            }
            return new Rect2I(new Vector2I(minX, minY), new Vector2I(maxX - minX + 1, maxY - minY + 1));
        }

        /// <summary>
        /// 显示右键上下文菜单（开辟地图 / 取消）。
        /// </summary>
        private void ShowExtendContextMenu()
        {
            if (_contextMenu == null)
            {
                _contextMenu = new PopupMenu();
                _contextMenu.AddThemeFontSizeOverride("font_size", 18);
                _contextMenu.AddThemeConstantOverride("v_separation", 10);
                _contextMenu.AddThemeConstantOverride("h_separation", 12);
                _contextMenu.AddItem("开辟地图", 0);
                _contextMenu.AddSeparator();
                _contextMenu.AddItem("取消", 1);
                _contextMenu.IdPressed += OnContextMenuItemSelected;
                _contextMenu.HideOnItemSelection = true;
                _contextMenu.HideOnCheckableItemSelection = true;
                AddChild(_contextMenu);
            }
            else
            {
                _contextMenu.Clear();
                _contextMenu.AddItem("开辟地图", 0);
                _contextMenu.AddSeparator();
                _contextMenu.AddItem("取消", 1);
            }

            _contextMenu.MinSize = new Vector2I(160, 80);

            var mouseScreenPos = GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            _contextMenu.Position = new Vector2I((int)mouseScreenPos.X, (int)mouseScreenPos.Y);
            _contextMenu.Popup();
        }

        private void OnContextMenuItemSelected(long id)
        {
            if (id == 0)
            {
                ShowExtendMapConfirmation();
            }
            // id == 1 或其他：取消，无需处理
        }

        /// <summary>
        /// 弹出二次确认对话框，确认后执行地图扩展。
        /// </summary>
        private void ShowExtendMapConfirmation()
        {
            if (GridManager == null)
                return;

            var cellsToAdd = new List<Vector2I>();
            foreach (var pos in SelectedCells.Keys)
            {
                if (!GridManager.IsInBounds(pos))
                    cellsToAdd.Add(pos);
            }

            if (cellsToAdd.Count == 0)
            {
                ShowToast("选区内没有可扩展的新格子", Colors.Yellow);
                return;
            }

            var selectionBounds = GetSelectionBounds();
            var mapBounds = GridManager.MapBounds;
            var newBounds = mapBounds.Merge(selectionBounds);

            var inBoundsCount = SelectedCells.Count - cellsToAdd.Count;

            var confirm = new ConfirmationDialog();
            confirm.Title = "开辟地图";
            var dialogText = $"将地图边界从 {GridManager.MapWidth}x{GridManager.MapHeight} 扩展至 {newBounds.Size.X}x{newBounds.Size.Y}。\n" +
                             $"新增 {cellsToAdd.Count} 个地图外格子，默认地形为普通。\n";
            if (inBoundsCount > 0)
                dialogText += $"选区内还有 {inBoundsCount} 个已有格子，不会被修改。\n";
            dialogText += "\n确定吗？";
            confirm.DialogText = dialogText;
            confirm.Confirmed += () =>
            {
                DoExtendMap(cellsToAdd);
                confirm.QueueFree();
            };
            confirm.Canceled += () => confirm.QueueFree();
            AddChild(confirm);
            confirm.PopupCentered();
        }

        /// <summary>
        /// 深拷贝完整 GridData，用于地图扩展命令的快照。
        /// </summary>
        private Dictionary<Vector2I, GridCell> DeepCopyGridData(Dictionary<Vector2I, GridCell> source)
        {
            var copy = new Dictionary<Vector2I, GridCell>();
            foreach (var kvp in source)
            {
                var cell = kvp.Value;
                var newCell = new GridCell(cell.Pos.X, cell.Pos.Y);
                newCell.Uid = cell.Uid;
                newCell.TerrainType = cell.TerrainType;
                newCell.DecorationType = cell.DecorationType;
                newCell.Height = cell.Height;
                newCell.CustomData = cell.CustomData;
                newCell.TerrainConfig = cell.TerrainConfig;
                copy[kvp.Key] = newCell;
            }
            return copy;
        }

        /// <summary>
        /// 执行地图扩展，并在扩展后将相机移动到新地图中心。
        /// </summary>
        private void DoExtendMap(List<Vector2I> cellsToAdd)
        {
            if (GridManager == null)
            {
                GD.PushError("[MapEditor.DoExtendMap] GridManager is null");
                return;
            }

            GD.Print($"[MapEditor.DoExtendMap] 扩展前 gridData={GridManager.GridData.Count}, cellsToAdd={cellsToAdd.Count}");

            // 保存扩展前状态，用于撤销
            var cmd = new ExtendMapCommand
            {
                OldGridData = DeepCopyGridData(GridManager.GridData)
            };
            GD.Print($"[MapEditor.DoExtendMap] OldGridData 已深拷贝, count={cmd.OldGridData.Count}");

            var err = GridManager.ExtendMap(cellsToAdd);
            GD.Print($"[MapEditor.DoExtendMap] ExtendMap 返回 {err}, 扩展后 gridData={GridManager.GridData.Count}");
            if (err == Error.Ok)
            {
                cmd.NewGridData = DeepCopyGridData(GridManager.GridData);
                GD.Print($"[MapEditor.DoExtendMap] NewGridData 已深拷贝, count={cmd.NewGridData.Count}");

                _undoStack.Add(cmd);
                if (_undoStack.Count > MaxUndoSteps)
                    _undoStack.RemoveAt(0);
                _redoStack.Clear();
                GD.Print($"[MapEditor.DoExtendMap] 命令已加入撤销栈, undoCount={_undoStack.Count}");

                SelectedCells.Clear();
                QueueRedraw();

                // 将相机移动到新地图中心
                if (Camera != null)
                {
                    var bounds = GridManager.MapBounds;
                    var mapCenter = GridManager.GlobalPosition +
                                    new Vector2(
                                        (bounds.Position.X + bounds.Size.X / 2.0f) * GridManager.GridSize,
                                        (bounds.Position.Y + bounds.Size.Y / 2.0f) * GridManager.GridSize);
                    Camera.GlobalPosition = mapCenter;
                }

                ShowToast($"地图已扩展至 {GridManager.MapWidth}x{GridManager.MapHeight}");
                GD.Print($"[MapEditor] 地图扩展成功: {GridManager.MapWidth}x{GridManager.MapHeight}, origin=({GridManager.GridOrigin.X},{GridManager.GridOrigin.Y})");
            }
            else
            {
                SelectedCells.Clear();
                QueueRedraw();
                ShowToast("扩展失败", Colors.Red);
                GD.PushError($"[MapEditor] 地图扩展失败: {err}");
            }
        }
    }
}
