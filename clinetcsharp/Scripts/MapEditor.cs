using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图编辑器 (重构版)
    /// 选中+属性设置模式
    /// </summary>
    [GlobalClass]
    public partial class MapEditor : Node2D
    {
        // 节点引用
        /// <summary>当前活动的 GridManager：编辑模式下返回独立的 _editGridManager，游戏模式下返回 _gameGridManager</summary>
        public GridManager? GridManager => IsEditing ? _editGridManager : _gameGridManager;
        private GridManager? _gameGridManager;
        private GridManager? _editGridManager;
        public CameraController? Camera { get; private set; }
        public Node2D? Player { get; private set; }

        // 编辑状态
        public bool IsEditing { get; private set; } = false;

        // 进入编辑模式前的游戏状态快照（用于退出时恢复）
        private bool _wasPlayerVisible;
        private bool _wasMonsterPatrolOverlayVisible;
        private bool _wasFunctionBarVisible;
        private bool _wasSkillBarVisible;
        private bool _wasMinimapVisible;
        private List<DraggablePanel> _wasVisiblePanels = new();

        // 选中系统
        public Dictionary<Vector2I, bool> SelectedCells { get; private set; } = new Dictionary<Vector2I, bool>();  // Vector2i -> bool
        public bool IsSelecting { get; private set; } = false;       // 是否正在拖拽选择
        public Vector2I SelectionStart { get; private set; }        // 框选起始格子
        public Vector2I SelectionEnd { get; private set; }          // 框选结束格子

        // 鼠标悬停格子跟踪
        private Vector2I _hoveredGridPos = new Vector2I(-1, -1);   // 当前鼠标悬停的网格坐标（-1,-1 表示无）

        // 框选开始时是否按住了 Ctrl（整个选择过程保持该语义）
        private bool _selectionStartedWithCtrl = false;

        // 右键上下文菜单
        private PopupMenu _contextMenu;

        // 当前设置的属性（应用到选中格子）
        public int PaintTerrain { get; set; } = 0;
        public int PaintDecoration { get; set; } = BuildingType.GetConfigBaseId(BuildingType.House) + 1; // 默认房舍 build_cfg_id=10001
        /// <summary>刷地形工具当前选中的"地形类建筑" decoration ID（如树、草地、水、岩石）</summary>
        public int PaintTerrainDecoration { get; set; } = BuildingType.GetConfigBaseId(BuildingType.House); // 默认树 10000

        // 编辑器工具模式（只保留刷地形和放置建筑）
        public enum EditorTool
        {
            PaintTerrain = 0,
            PlaceDecoration = 1,
        }
        public EditorTool CurrentTool { get; set; } = EditorTool.PaintTerrain;

        // 编辑期装饰摆件管理器（与游戏运行时管理器隔离）
        private MapDecorationManager? _editDecorationManager;

        // 摆件拖拽状态
        private enum DragMode { None, FromPalette, MovePlaced }
        private DragMode _dragMode = DragMode.None;
        private int _dragDecorationType = 0;
        private Vector2I _dragSourceGridPos = new Vector2I(-1, -1);
        private Control? _dragGhost;

        // UI引用
        private Control _editorPanel;

        // 鼠标悬停提示框
        private CanvasLayer _hoverTooltipCanvasLayer;
        private PanelContainer _hoverTooltipPanel;
        private Label _hoverTooltipLabel;
        private float _hoverTooltipWidth = 200f;

        /// <summary>鼠标悬停提示框宽度（可由调试面板设置）</summary>
        public float HoverTooltipWidth
        {
            get => _hoverTooltipWidth;
            set
            {
                _hoverTooltipWidth = value;
                if (_hoverTooltipLabel != null)
                    _hoverTooltipLabel.CustomMinimumSize = new Vector2(value, 0);
            }
        }

        // 撤销历史（差分命令模式：只记录被修改格子的旧值，不再深拷贝全网格）
        private List<EditCommand> _undoStack = new();
        private List<EditCommand> _redoStack = new();
        public const int MaxUndoSteps = 20;

        /// <summary>编辑命令基类</summary>
        private abstract class EditCommand
        {
            public abstract void Undo(GridManager grid);
            public abstract void Redo(GridManager grid);
        }

        /// <summary>
        /// 地形编辑命令 — 记录一次编辑操作中被修改格子的旧地形值。
        /// 仅存储 (位置, 旧值) 差分，而非完整网格深拷贝。
        /// </summary>
        private class TerrainEditCommand : EditCommand
        {
            /// <summary>应用此命令后的新地形值（用于 Redo）</summary>
            public int NewTerrainType;
            /// <summary>被修改的格子列表及其旧地形值</summary>
            public List<(Vector2I Pos, int OldTerrainType)> Changes = new();

            public override void Undo(GridManager grid)
            {
                foreach (var (pos, oldType) in Changes)
                {
                    if (!grid.IsInBounds(pos)) continue;
                    var cell = grid.GetCell(pos);
                    if (cell == null) continue;
                    cell.TerrainType = oldType;
                    cell.TerrainConfig = TerrainConfigUtil.Get(oldType);
                }
                grid.NotifyTerrainChanged();
                GD.Print($"[MapEditor.Undo] 恢复 {Changes.Count} 个格子的地形");
            }

            public override void Redo(GridManager grid)
            {
                foreach (var (pos, _) in Changes)
                {
                    if (!grid.IsInBounds(pos)) continue;
                    var cell = grid.GetCell(pos);
                    if (cell == null) continue;
                    cell.TerrainType = NewTerrainType;
                    cell.TerrainConfig = TerrainConfigUtil.Get(NewTerrainType);
                }
                grid.NotifyTerrainChanged();
                GD.Print($"[MapEditor.Redo] 重做 {Changes.Count} 个格子的地形为 {NewTerrainType}");
            }
        }

        /// <summary>
        /// 装饰编辑命令 — 记录一次装饰编辑操作中被修改格子的旧装饰值与新装饰值。
        /// 支持放置、移动、删除。
        /// </summary>
        private class DecorationEditCommand : EditCommand
        {
            public List<(Vector2I Pos, int OldDecorationType, int NewDecorationType)> Changes = new();

            public override void Undo(GridManager grid)
            {
                foreach (var (pos, oldType, _) in Changes)
                {
                    if (!grid.IsInBounds(pos)) continue;
                    var cell = grid.GetCell(pos);
                    if (cell == null) continue;
                    cell.DecorationType = oldType;
                }
                grid.NotifyTerrainChanged();
                grid.SyncDecorations();
                GD.Print($"[MapEditor.Undo] 恢复 {Changes.Count} 个格子的装饰");
            }

            public override void Redo(GridManager grid)
            {
                foreach (var (pos, _, newType) in Changes)
                {
                    if (!grid.IsInBounds(pos)) continue;
                    var cell = grid.GetCell(pos);
                    if (cell == null) continue;
                    cell.DecorationType = newType;
                }
                grid.NotifyTerrainChanged();
                grid.SyncDecorations();
                GD.Print($"[MapEditor.Redo] 重做 {Changes.Count} 个格子的装饰");
            }
        }

        /// <summary>
        /// 地图扩展命令 — 保存扩展前后的完整地图状态（深拷贝）。
        /// 使用稀疏字典，MapBounds 从 GridData 推导。
        /// </summary>
        private class ExtendMapCommand : EditCommand
        {
            public Dictionary<Vector2I, GridCell> OldGridData = new();
            public Dictionary<Vector2I, GridCell> NewGridData = new();

            public override void Undo(GridManager grid)
            {
                ApplySnapshot(grid, OldGridData);
                GD.Print("[MapEditor.ExtendMapCommand] 撤销扩展，恢复旧地图状态");
            }

            public override void Redo(GridManager grid)
            {
                ApplySnapshot(grid, NewGridData);
                GD.Print("[MapEditor.ExtendMapCommand] 重做扩展");
            }

            private static void ApplySnapshot(GridManager grid, Dictionary<Vector2I, GridCell> data)
            {
                GD.Print($"[MapEditor.ExtendMapCommand.ApplySnapshot] 恢复前 gridData={grid.GridData.Count}, 恢复后 data={data.Count}");
                grid.GridData = data;
                grid.RecalculateMapBounds();
                grid.SaveMapBoundsToConfig();
                grid.SaveCurrentMap();

                grid.UpdateGridShaderOverlay();
                grid.NotifyTerrainChanged();
                grid.SyncBackgroundSize();
                grid.QueueRedraw();
                GD.Print($"[MapEditor.ExtendMapCommand.ApplySnapshot] 恢复完成, MapBounds={grid.MapBounds}");
            }
        }

        /// <summary>
        /// 选区变更命令 — 记录一次选择/框选操作前后的选中格子集合。
        /// 使 Ctrl+Z 可以撤销选区变化。
        /// </summary>
        private class SelectionEditCommand : EditCommand
        {
            public Dictionary<Vector2I, bool> OldSelection = new();
            public Dictionary<Vector2I, bool> NewSelection = new();
            private readonly MapEditor _editor;

            public SelectionEditCommand(MapEditor editor)
            {
                _editor = editor;
            }

            public override void Undo(GridManager grid)
            {
                _editor.SelectedCells = new Dictionary<Vector2I, bool>(OldSelection);
                _editor.UpdateSelectionLabel();
                _editor.QueueRedraw();
                GD.Print($"[MapEditor.SelectionEditCommand] 撤销选区，恢复 {OldSelection.Count} 个格子");
            }

            public override void Redo(GridManager grid)
            {
                _editor.SelectedCells = new Dictionary<Vector2I, bool>(NewSelection);
                _editor.UpdateSelectionLabel();
                _editor.QueueRedraw();
                GD.Print($"[MapEditor.SelectionEditCommand] 重做选区，恢复 {NewSelection.Count} 个格子");
            }
        }

        // 选择历史（用于右键撤销）
        private List<Dictionary<Vector2I, bool>> _selectionHistory = new List<Dictionary<Vector2I, bool>>();  // 每次选择操作前保存选中状态
        public const int MaxSelectionHistory = 10;

        // 框选/选择操作前的选区快照，用于生成 SelectionEditCommand
        private Dictionary<Vector2I, bool> _selectionBeforeDrag = new();

        public override async void _Ready()
        {
            // 循环等待关键节点出现（最多10帧），避免 _Ready 时机差异导致获取失败
            for (int i = 0; i < 10; i++)
            {
                _gameGridManager = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
                Camera = GetTree()?.GetFirstNodeInGroup("camera") as CameraController;
                Player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
                if (_gameGridManager != null && Camera != null)
                    break;
                await ToSignal(GetTree(), "process_frame");
            }
            GD.Print($"[MapEditor] 初始化完成 GridManager={(_gameGridManager != null ? "OK" : "NULL")} Camera={(Camera != null ? "OK" : "NULL")}");


            // 加入 map_editor 组，供调试面板通过 GetTree().GetFirstNodeInGroup 访问
            AddToGroup("map_editor");

            // 确保选区高亮绘制在 GridShaderOverlay 之上
            ZIndex = 10;
            ZAsRelative = false;

            // 建筑配置变化时刷新建筑图鉴
            DecorationConfigUtil.ProfilesChanged += OnDecorationProfilesChanged;

            // 自动化测试模式：检测到 --test-grid-visibility 参数时自动进入编辑模式
            foreach (var arg in OS.GetCmdlineArgs())
            {
                if (arg == "--test-grid-visibility")
                {
                    GD.Print("[MapEditor] Auto-test mode detected, opening editor...");
                    ToggleEditor();
                    _ = RunAutoTestScreenshot();
                    break;
                }
            }
        }

        public override void _ExitTree()
        {
            UIInputPolicy.Instance?.UnregisterUiNode(this);
            if (Camera is Node camNode)
                UIInputPolicy.Instance?.UnregisterUiNode(camNode);
            if (_editorPanel != null)
                UIInputPolicy.Instance?.UnregisterUiNode(_editorPanel);
            if (_editGridManager != null)
                UIInputPolicy.Instance?.UnregisterUiNode(_editGridManager);
            if (_editDecorationManager != null)
                UIInputPolicy.Instance?.UnregisterUiNode(_editDecorationManager);
        }

        // 网格修复版本号，每次修改后递增，用于验证客户端加载的是最新代码
        public const string GridFixVersion = "v4.6"; // edit mode fixed 2px screen line width

        public override void _Process(double delta)
        {
            if (IsEditing && _editorPanel != null)
            {
                var diagLabel = _editorPanel.GetNodeOrNull<Label>("VBoxContainer/DiagLabel");
                if (diagLabel != null && GridManager != null && Camera != null)
                {
                    var zoom = Camera.Zoom.X;
                    var (lineWidthWorld, lineColor) = GridManager.ComputeGridLineRenderStylePublic(zoom);
                    var screenLineWidth = lineWidthWorld * zoom;
                    var mode = "整体-shader";
                    var aaSoftness = GridManager.GetGridAntiAliasSoftness();
                    diagLabel.Text = $"[修复版本 {GridFixVersion}]\n" +
                                     $"zoom={zoom:F2} 实际线宽={screenLineWidth:F2}px 模式={mode}\n" +
                                     $"颜色=({lineColor.R:F2},{lineColor.G:F2},{lineColor.B:F2},{lineColor.A:F2}) 柔化={aaSoftness:F1}x";
                }

                // 鼠标悬停格子检测（鼠标不在 UI 上时才检测）
                var isOverUi = UiUtils.IsMouseOverAnyUi(GetViewport());
                if (!isOverUi && GridManager != null)
                {
                    var mouseLocalPos = GridManager.ToLocal(GetGlobalMousePosition());
                    var gridPos = GridManager.WorldToGrid(mouseLocalPos);

                    if (gridPos != _hoveredGridPos)
                    {
                        _hoveredGridPos = gridPos;
                        UpdateHoverInfo();
                    }
                }
                else if (_hoveredGridPos != new Vector2I(-1, -1))
                {
                    _hoveredGridPos = new Vector2I(-1, -1);
                    UpdateHoverInfo();
                }

                // 拖拽中：更新幽灵位置并轮询鼠标释放
                if (_dragMode != DragMode.None)
                {
                    UpdateDragGhostPosition();
                    if (!Input.IsMouseButtonPressed(MouseButton.Left))
                    {
                        bool overEditorPanel = IsMouseOverEditorPanel();
                        EndDrag(overEditorPanel);
                    }
                }
            }
            else
            {
                // 不在编辑模式时隐藏悬停提示框
                if (_hoverTooltipPanel != null)
                    _hoverTooltipPanel.Visible = false;
            }
        }

        public override void _Input(InputEvent @event)
        {
            // 切换编辑模式 (E键)
            if (Input.IsActionJustPressed("toggle_editor"))
            {
                ToggleEditor();
                return;
            }

            if (!IsEditing)
                return;

            // ESC退出编辑模式
            if (Input.IsActionJustPressed("ui_cancel"))
            {
                SetEditMode(false);
                return;
            }

            // 撤销 (Ctrl+Z) — 只在按键按下事件触发，避免重复
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Z, CtrlPressed: true })
            {
                Undo();
                return;
            }

            // 全选 (Ctrl+A) — 在刷地形工具下有效
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.A, CtrlPressed: true })
            {
                if (CurrentTool == EditorTool.PaintTerrain)
                    SelectAll();
                return;
            }

            // Delete 键：只在放置建筑模式下删除悬停建筑
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Delete })
            {
                if (CurrentTool == EditorTool.PlaceDecoration)
                    DeleteSelectedDecorations();
                return;
            }

            // Ctrl+Shift+B：在放建筑工具下打开 DebugPanel 建筑工坊
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.B, CtrlPressed: true, ShiftPressed: true })
            {
                if (CurrentTool == EditorTool.PlaceDecoration)
                {
                    OnOpenDecorationWorkshop();
                    GetViewport()?.SetInputAsHandled();
                }
                return;
            }

            // 检查鼠标是否在 UI 上，如果在 UI 上则不处理地图编辑事件
            if (UiUtils.IsMouseOverAnyUi(GetViewport()))
                return;

            // 鼠标处理
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        if (CurrentTool == EditorTool.PaintTerrain)
                        {
                            // 刷地形工具下：按住 Ctrl 启动框选；未按 Ctrl 不处理，留给相机拖视野。
                            if (mb.CtrlPressed)
                            {
                                StartSelection(mb);
                            }
                        }
                        // 放置建筑模式下：
                        // - 点击已放置建筑：由 MapDecoration._Input 处理并触发移动拖拽（仅在放建筑工具）
                        // - 点击空白地图：留给相机控制器拖动视野
                    }
                    else
                    {
                        if (IsSelecting)
                        {
                            EndSelection();
                        }
                    }
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    if (mb.Pressed && CurrentTool == EditorTool.PaintTerrain)
                    {
                        // 右键：如果选区包含越界格子，弹出开辟菜单；否则撤销选择
                        if (HasOutOfBoundsSelection())
                            ShowExtendContextMenu();
                        else
                            UndoSelection();
                    }
                }
            }

            if (@event is InputEventMouseMotion mm)
            {
                if (IsSelecting && CurrentTool == EditorTool.PaintTerrain)
                {
                    UpdateSelection(mm);
                }
            }
        }

        public override void _Draw()
        {
            if (!IsEditing || GridManager == null)
                return;

            // 绘制选中的格子
            // 线宽根据相机zoom自适应，确保在屏幕上始终清晰可见
            float selectionLineWidth = 3.0f;
            if (Camera != null)
                selectionLineWidth = Mathf.Max(3.0f / Camera.Zoom.X, 1.0f);

            // 正常选中的格子：淡红色半透明填充 + 红色边框
            var selectionFillColor = new Color(1, 0, 0, 0.25f);
            // 越界选中的格子：橙色填充 + 橙色边框（表示可开辟区域）
            var outOfBoundsFillColor = new Color(1, 0.5f, 0, 0.35f);
            var outOfBoundsBorderColor = new Color(1, 0.6f, 0, 1.0f);

            foreach (var pos in SelectedCells.Keys)
            {
                var gridLocalPos = GridManager.GridToWorld(pos);
                var editorLocalPos = ToLocal(GridManager.ToGlobal(gridLocalPos));
                var rect = new Rect2(
                    editorLocalPos - new Vector2(GridManager.GridSize / 2.0f, GridManager.GridSize / 2.0f),
                    new Vector2(GridManager.GridSize, GridManager.GridSize)
                );

                bool inBounds = GridManager.IsInBounds(pos);
                DrawRect(rect, inBounds ? selectionFillColor : outOfBoundsFillColor, true);
                DrawRect(rect, inBounds ? Colors.Red : outOfBoundsBorderColor, false, selectionLineWidth);
            }

            // 绘制框选区域预览
            if (IsSelecting)
            {
                var minX = Mathf.Min(SelectionStart.X, SelectionEnd.X);
                var maxX = Mathf.Max(SelectionStart.X, SelectionEnd.X);
                var minY = Mathf.Min(SelectionStart.Y, SelectionEnd.Y);
                var maxY = Mathf.Max(SelectionStart.Y, SelectionEnd.Y);

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        var gridPos = new Vector2I(x, y);
                        var gridLocalPos = GridManager.GridToWorld(gridPos);
                        var editorLocalPos = ToLocal(GridManager.ToGlobal(gridLocalPos));
                        var rect = new Rect2(
                            editorLocalPos - new Vector2(GridManager.GridSize / 2.0f, GridManager.GridSize / 2.0f),
                            new Vector2(GridManager.GridSize, GridManager.GridSize)
                        );

                        bool inBounds = GridManager.IsInBounds(gridPos);
                        DrawRect(rect, inBounds ? new Color(1, 0, 0, 0.3f) : new Color(1, 0.5f, 0, 0.4f), true);
                    }
                }
            }

            // 绘制建筑拖拽 footprint 预览
            if (CurrentTool == EditorTool.PlaceDecoration && _dragMode != DragMode.None && _editGridManager != null)
            {
                var mouseLocalPos = _editGridManager.ToLocal(GetGlobalMousePosition());
                var dropAnchor = _editGridManager.WorldToGrid(mouseLocalPos);
                var (sx, sy) = GetDecorationSize(_dragDecorationType);
                var footprint = GetFootprintCells(dropAnchor, sx, sy);
                bool valid = true;
                var occupied = GetOccupiedFootprintCells(_dragSourceGridPos, sx, sy);
                foreach (var pos in footprint)
                {
                    if (!_editGridManager.IsInBounds(pos)) { valid = false; break; }
                    if (occupied.Contains(pos)) continue;
                    var cell = _editGridManager.GetCell(pos);
                    if (cell != null && cell.DecorationType != 0) { valid = false; break; }
                }

                var previewColor = valid ? new Color(0, 1, 0, 0.25f) : new Color(1, 0, 0, 0.35f);
                var borderColor = valid ? new Color(0, 1, 0, 0.8f) : new Color(1, 0, 0, 0.9f);
                float lineWidth = Camera != null ? Mathf.Max(2.0f / Camera.Zoom.X, 1.0f) : 2.0f;
                foreach (var pos in footprint)
                {
                    var gridLocalPos = GridManager.GridToWorld(pos);
                    var editorLocalPos = ToLocal(GridManager.ToGlobal(gridLocalPos));
                    var rect = new Rect2(
                        editorLocalPos - new Vector2(GridManager.GridSize / 2.0f, GridManager.GridSize / 2.0f),
                        new Vector2(GridManager.GridSize, GridManager.GridSize)
                    );
                    DrawRect(rect, previewColor, true);
                    DrawRect(rect, borderColor, false, lineWidth);
                }
            }
        }

        public void ToggleEditor()
        {
            SetEditMode(!IsEditing);
        }

        public void SetEditMode(bool enabled)
        {
            IsEditing = enabled;

            // 兜底：如果 _gameGridManager 之前获取失败，在这里重新尝试
            if (_gameGridManager == null)
            {
                _gameGridManager = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
                if (_gameGridManager == null)
                    GD.PushError("[MapEditor] SetEditMode: GridManager is still null!");
                else
                    GD.Print($"[MapEditor] SetEditMode: late-acquired GridManager={_gameGridManager.Name}");
            }
            // 不再直接修改 _gameGridManager 的编辑模式状态（由 Enter/Exit 处理独立实例）
            // _gameGridManager?.SetEditMode(enabled);  // 已移除：编辑模式使用独立 _editGridManager

            Camera?.SetEditorMode(enabled);

            // 控制调试面板的可见性（地图编辑器独立于调试面板）
            var debugPanel = GetTree().GetFirstNodeInGroup("debug_panel");
            if (debugPanel is CanvasItem ci)
            {
                ci.Visible = !enabled;
            }

            if (enabled)
            {
                EnterEditMode();
            }
            else
            {
                ExitEditMode();
            }
        }
    }
}
