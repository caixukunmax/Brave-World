using Godot;
using System.Collections.Generic;

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
        private System.Collections.Generic.List<DraggablePanel> _wasVisiblePanels = new();

        // 选中系统
        public System.Collections.Generic.Dictionary<Vector2I, bool> SelectedCells { get; private set; } = new System.Collections.Generic.Dictionary<Vector2I, bool>();  // Vector2i -> bool
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
        public int PaintDecoration { get; set; } = BuildingType.GetConfigBaseId(BuildingType.House); // 默认房舍 build_cfg_id=10000

        // 编辑器工具模式（只保留刷地形和放置建筑）
        public enum EditorTool
        {
            PaintTerrain = 0,
            PlaceDecoration = 1,
        }
        public EditorTool CurrentTool { get; set; } = EditorTool.PaintTerrain;

        // 刷地形子模式
        public enum TerrainPaintMode
        {
            Brush = 0,
            BoxSelect = 1,
        }
        public TerrainPaintMode CurrentTerrainPaintMode { get; set; } = TerrainPaintMode.Brush;

        // 笔刷模式下最近涂刷的格子，用于去重
        private Vector2I _lastBrushedGridPos = new Vector2I(int.MinValue, int.MinValue);

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
        private System.Collections.Generic.List<EditCommand> _undoStack = new();
        private System.Collections.Generic.List<EditCommand> _redoStack = new();
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
            public System.Collections.Generic.Dictionary<Vector2I, GridCell> OldGridData = new();
            public System.Collections.Generic.Dictionary<Vector2I, GridCell> NewGridData = new();

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

            private static void ApplySnapshot(GridManager grid, System.Collections.Generic.Dictionary<Vector2I, GridCell> data)
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
            public System.Collections.Generic.Dictionary<Vector2I, bool> OldSelection = new();
            public System.Collections.Generic.Dictionary<Vector2I, bool> NewSelection = new();
            private readonly MapEditor _editor;

            public SelectionEditCommand(MapEditor editor)
            {
                _editor = editor;
            }

            public override void Undo(GridManager grid)
            {
                _editor.SelectedCells = new System.Collections.Generic.Dictionary<Vector2I, bool>(OldSelection);
                _editor.UpdateSelectionLabel();
                _editor.QueueRedraw();
                GD.Print($"[MapEditor.SelectionEditCommand] 撤销选区，恢复 {OldSelection.Count} 个格子");
            }

            public override void Redo(GridManager grid)
            {
                _editor.SelectedCells = new System.Collections.Generic.Dictionary<Vector2I, bool>(NewSelection);
                _editor.UpdateSelectionLabel();
                _editor.QueueRedraw();
                GD.Print($"[MapEditor.SelectionEditCommand] 重做选区，恢复 {NewSelection.Count} 个格子");
            }
        }

        /// <summary>
        /// 建筑图鉴列表项：显示颜色块 + 建筑名称，ButtonDown 时触发从面板拖拽。
        /// </summary>
        private partial class BuildingListItem : Button
        {
            private readonly MapEditor _editor;
            private readonly int _decorationTypeId;
            private readonly DecorationConfig _cfg;
            public int DecorationTypeId => _decorationTypeId;
            public DecorationConfig Config => _cfg;

            public BuildingListItem(MapEditor editor, DecorationConfig cfg)
            {
                _editor = editor;
                _cfg = cfg;
                _decorationTypeId = cfg.Id;
                CustomMinimumSize = new Vector2(0, 36);
                MouseFilter = MouseFilterEnum.Stop;
                ClipText = true;
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                AddThemeFontSizeOverride("font_size", 12);
                AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
                SetDecorationStyle(_cfg);
                ButtonDown += () => _editor.StartPaletteDrag(_decorationTypeId);
                TooltipText = _cfg.DisplayName;
            }

            private void SetDecorationStyle(DecorationConfig cfg)
            {
                var iconColor = cfg.Color;
                var normal = new StyleBoxFlat
                {
                    BgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f),
                    BorderColor = new Color(0.3f, 0.3f, 0.3f),
                    BorderWidthBottom = 1,
                    BorderWidthLeft = 1,
                    BorderWidthRight = 1,
                    BorderWidthTop = 1,
                };
                var hover = new StyleBoxFlat
                {
                    BgColor = new Color(0.25f, 0.25f, 0.25f, 0.9f),
                    BorderColor = new Color(0.5f, 0.5f, 0.5f),
                    BorderWidthBottom = 1,
                    BorderWidthLeft = 1,
                    BorderWidthRight = 1,
                    BorderWidthTop = 1,
                };
                var pressed = new StyleBoxFlat
                {
                    BgColor = new Color(0.35f, 0.35f, 0.35f, 0.95f),
                    BorderColor = Colors.White,
                    BorderWidthBottom = 1,
                    BorderWidthLeft = 1,
                    BorderWidthRight = 1,
                    BorderWidthTop = 1,
                };
                AddThemeStyleboxOverride("normal", normal);
                AddThemeStyleboxOverride("hover", hover);
                AddThemeStyleboxOverride("pressed", pressed);

                // 用文本前缀加一个彩色标记来示意建筑颜色
                Text = $"  {cfg.DisplayName}";
            }

            public override void _Draw()
            {
                base._Draw();
                var iconRect = new Rect2(new Vector2(6, 8), new Vector2(20, 20));
                DrawRect(iconRect, _cfg.Color, true);
                DrawRect(iconRect, _cfg.BorderColor, false, 1.5f);
            }
        }

        // 选择历史（用于右键撤销选择）
        private List<System.Collections.Generic.Dictionary<Vector2I, bool>> _selectionHistory = new List<System.Collections.Generic.Dictionary<Vector2I, bool>>();  // 每次选择操作前保存选中状态
        public const int MaxSelectionHistory = 10;

        // 框选/选择操作前的选区快照，用于生成 SelectionEditCommand
        private System.Collections.Generic.Dictionary<Vector2I, bool> _selectionBeforeDrag = new();

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

                // 刷地形笔刷模式：按住左键拖动时持续涂刷
                if (CurrentTool == EditorTool.PaintTerrain && CurrentTerrainPaintMode == TerrainPaintMode.Brush
                    && Input.IsMouseButtonPressed(MouseButton.Left) && !isOverUi)
                {
                    BrushTerrainAtMouse();
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

            // 全选 (Ctrl+A) — 只在刷地形框选模式下有效
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.A, CtrlPressed: true })
            {
                if (CurrentTool == EditorTool.PaintTerrain && CurrentTerrainPaintMode == TerrainPaintMode.BoxSelect)
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
                            if (CurrentTerrainPaintMode == TerrainPaintMode.BoxSelect)
                            {
                                StartSelection(mb);
                            }
                            else
                            {
                                // 笔刷模式：立即刷当前格子，并标记开始涂刷
                                _lastBrushedGridPos = new Vector2I(int.MinValue, int.MinValue);
                                BrushTerrainAtMouse();
                            }
                        }
                        // 放置建筑模式下：
                        // - 点击已放置建筑：由 MapDecoration._Input 处理并触发移动拖拽
                        // - 点击空白地图：留给相机控制器拖动视野
                    }
                    else
                    {
                        if (IsSelecting)
                        {
                            EndSelection();
                        }
                        _lastBrushedGridPos = new Vector2I(int.MinValue, int.MinValue);
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
                if (IsSelecting && CurrentTool == EditorTool.PaintTerrain && CurrentTerrainPaintMode == TerrainPaintMode.BoxSelect)
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

        private async System.Threading.Tasks.Task RunAutoTestScreenshot()
        {
            // 1. 等待编辑器完全加载
            GD.Print("[MapEditor] Auto-test: waiting 3s for editor to fully load...");
            await ToSignal(GetTree().CreateTimer(3.0f, true), "timeout");

            var camera = GetTree().GetFirstNodeInGroup("camera") as Camera2D;
            var viewport = GetViewport();

            // 2. 在多个关键 zoom 级别分别截图，覆盖所有边界情况
            float[] testZooms = new float[] { 1.0f, 0.7f, 0.5f, 0.4f, 0.3f, 0.2f };
            foreach (var zoom in testZooms)
            {
                if (camera != null)
                {
                    camera.Zoom = new Vector2(zoom, zoom);
                    GD.Print($"[MapEditor] Auto-test: set zoom={zoom:F2}");
                }

                // 等待画面稳定（GridManager 检测到 zoom 变化后会 QueueRedraw）
                await ToSignal(GetTree().CreateTimer(1.0f, true), "timeout");

                var img = viewport.GetTexture().GetImage();
                var path = ProjectSettings.GlobalizePath($"user://auto_test_grid_zoom_{zoom:F2}.png");
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                img.SavePng(path);
                GD.Print($"[MapEditor] Auto-test: screenshot saved to {path}");
            }

            await ToSignal(GetTree().CreateTimer(0.5f, true), "timeout");
            GD.Print("[MapEditor] Auto-test: quitting...");
            GetTree().Quit();
        }

        private void EnterEditMode()
        {
            GD.Print("[MapEditor] 进入编辑模式 — 冻结游戏状态，重新加载地图数据");

            // 1. 保存游戏状态快照
            _wasPlayerVisible = Player?.Visible ?? false;

            var patrolOverlay = GetTree().GetFirstNodeInGroup("monster_patrol_overlay") as MonsterPatrolOverlay;
            _wasMonsterPatrolOverlayVisible = patrolOverlay?.OverlayEnabled ?? false;

            // 2. 暂停游戏树（冻结所有 _Process/_PhysicsProcess），
            //    并通过 UIInputPolicy 保证编辑器相关节点仍可处理输入
            UIInputPolicy.Instance?.RegisterUiNode(this);
            if (Camera is Node camNode)
                UIInputPolicy.Instance?.RegisterUiNode(camNode);
            UIInputPolicy.Instance?.PauseGame();

            // 3. 隐藏玩家、所有怪物实例、所有NPC实例、巡逻覆盖层、宝箱、掉落物
            if (Player != null)
                Player.Visible = false;

            var monsterMgr = GetTree().GetFirstNodeInGroup("monster_manager") as MonsterManager;
            monsterMgr?.SetAllMonstersVisible(false);

            var npcMgr = GetTree().GetFirstNodeInGroup("npc_manager") as NpcManager;
            npcMgr?.SetAllNpcsVisible(false);
            npcMgr?.CloseInteractMenu();

            patrolOverlay?.SetOverlayEnabled(false);

            var chestMgr = GetTree().GetFirstNodeInGroup("chest_manager") as ChestManager;
            chestMgr?.SetAllChestsVisible(false);

            var dropMgr = GetTree().GetFirstNodeInGroup("drop_manager") as DropManager;
            dropMgr?.SetAllDropsVisible(false);

            var decMgr = GetTree().GetFirstNodeInGroup("map_decoration_manager") as MapDecorationManager;
            decMgr?.SetAllDecorationsVisible(false);

            // 4. 隐藏所有与地图编辑器无关的 UI（功能按钮栏、技能栏、所有面板）
            var funcBar = GetTree()?.GetFirstNodeInGroup("function_bar") as CanvasItem;
            if (funcBar != null)
            {
                _wasFunctionBarVisible = funcBar.Visible;
                funcBar.Visible = false;
            }

            var skillBar = GetTree()?.GetFirstNodeInGroup("skill_bar") as CanvasItem;
            if (skillBar != null)
            {
                _wasSkillBarVisible = skillBar.Visible;
                skillBar.Visible = false;
            }

            _wasVisiblePanels.Clear();
            var panelMgr = PanelManager.Instance;
            if (panelMgr != null)
            {
                // 先快照当前可见的面板
                var uiCanvas = panelMgr.GetParent() as CanvasLayer;
                if (uiCanvas != null)
                {
                    foreach (var child in uiCanvas.GetChildren())
                    {
                        if (child is DraggablePanel dp && dp.Visible)
                            _wasVisiblePanels.Add(dp);
                    }
                }
                // 然后全部隐藏
                panelMgr.HideAll();
            }

            // ===== 核心变更：创建独立编辑 GridManager，彻底隔离编辑副作用 =====
            if (_gameGridManager != null)
            {
                // 防御性清理：防止上一次编辑模式异常退出导致旧实例残留
                if (_editGridManager != null && IsInstanceValid(_editGridManager))
                {
                    GD.Print("[MapEditor] 清理残留的编辑 GridManager");
                    _editGridManager.QueueFree();
                    _editGridManager = null;
                }

                // 隐藏游戏 GridManager（只读，编辑期间不被修改）
                _gameGridManager.Visible = false;

                // 创建编辑用 GridManager，完全复制渲染参数
                _editGridManager = new GridManager();
                _editGridManager.Name = "EditGridManager";
                _editGridManager.ZIndex = _gameGridManager.ZIndex;
                _editGridManager.ZAsRelative = _gameGridManager.ZAsRelative;

                // 复制地图和渲染参数（MapBounds 由 GridData 推导，无需复制）
                _editGridManager.GridSize = _gameGridManager.GridSize;
                _editGridManager.CurrentMapName = _gameGridManager.CurrentMapName;
                _editGridManager.LineColor = _gameGridManager.LineColor;
                _editGridManager.LineWidth = _gameGridManager.LineWidth;
                _editGridManager.AutoLineWidth = _gameGridManager.AutoLineWidth;
                _editGridManager.LineWidthScale = _gameGridManager.LineWidthScale;
                _editGridManager.MinScreenLineWidth = _gameGridManager.MinScreenLineWidth;
                _editGridManager.MaxScreenLineWidth = _gameGridManager.MaxScreenLineWidth;
                _editGridManager.GridAntiAliasSoftness = _gameGridManager.GridAntiAliasSoftness;
                _editGridManager.AdaptiveCalibrationEnabled = _gameGridManager.AdaptiveCalibrationEnabled;
                _editGridManager.RefZoomA = _gameGridManager.RefZoomA;
                _editGridManager.RefWidthA = _gameGridManager.RefWidthA;
                _editGridManager.RefZoomB = _gameGridManager.RefZoomB;
                _editGridManager.RefWidthB = _gameGridManager.RefWidthB;
                _editGridManager.ResponsiveMode = _gameGridManager.ResponsiveMode;
                _editGridManager.VisibleGridsX = _gameGridManager.VisibleGridsX;
                _editGridManager.MinGridSize = _gameGridManager.MinGridSize;
                _editGridManager.MaxGridSize = _gameGridManager.MaxGridSize;
                _editGridManager.ShowOutsideMapGray = _gameGridManager.ShowOutsideMapGray;
                _editGridManager.OutsideMapColor = _gameGridManager.OutsideMapColor;
                _editGridManager.ShowGridCoords = _gameGridManager.ShowGridCoords;
                _editGridManager.ShowCellUids = _gameGridManager.ShowCellUids;
                _editGridManager.ShowTerrainLabels = true; // 编辑模式默认显示地形标签

                // 编辑模式标志（这是编辑地图独有的，游戏 GridManager 不再被修改）
                _editGridManager.IsEditMode = true;

                // 添加到场景树（放在原 GridManager 的父节点下，保持坐标一致）
                _gameGridManager.GetParent()?.AddChild(_editGridManager);
                _editGridManager.GlobalPosition = _gameGridManager.GlobalPosition;

                // 编辑用 GridManager 需在暂停时继续运行 _Process 以响应 zoom/线宽变化
                UIInputPolicy.Instance?.RegisterUiNode(_editGridManager);

                // 显式加载地图数据（防御性：确保 _Ready() 加载成功，若失败则再次尝试）
                var loaded = _editGridManager.LoadMap(_editGridManager.CurrentMapName);
                GD.Print($"[MapEditor] 创建独立编辑 GridManager: {_editGridManager.CurrentMapName} {_editGridManager.MapWidth}x{_editGridManager.MapHeight}, LoadMap={loaded}");

                // 创建编辑期装饰摆件管理器，与游戏运行时管理器隔离
                _editDecorationManager = new MapDecorationManager();
                _editDecorationManager.Name = "EditDecorationManager";
                _editDecorationManager.AddToGroup("edit_decoration_manager");
                _editDecorationManager.SpawnEditable = true;
                _editDecorationManager.GridSize = _editGridManager.GridSize;
                _editGridManager.AddChild(_editDecorationManager);
                _editDecorationManager.SpawnDecorations(_editGridManager.GridData);

                // 编辑期装饰管理器也需在暂停时继续处理
                UIInputPolicy.Instance?.RegisterUiNode(_editDecorationManager);

                // 订阅摆件拖动请求
                MapDecoration.DecorationDragRequested += OnDecorationDragRequested;
            }

            // 5. 清空编辑历史
            SelectedCells.Clear();
            IsSelecting = false;
            _undoStack.Clear();
            _redoStack.Clear();
            _selectionHistory.Clear();

            QueueRedraw();
            ShowEditorUi();

            // 刷新地图列表（必须在 ShowEditorUi 之后，确保面板已创建）
            RefreshMapList();
        }

        private void ExitEditMode()
        {
            GD.Print("[MapEditor] 退出编辑模式 — 保存地图，恢复游戏状态");

            // ===== 核心变更：保存编辑结果并销毁独立编辑 GridManager =====
            if (_editGridManager != null)
            {
                var mapName = _editGridManager.CurrentMapName;
                var err = _editGridManager.SaveCurrentMap();
                if (err == Error.Ok)
                {
                    GD.Print($"[MapEditor] 地图 '{mapName}' 保存成功");
                }
                else
                {
                    GD.PushError($"[MapEditor] 地图 '{mapName}' 保存失败: {err}");
                    ShowToast($"保存失败: {err}", Colors.Red);
                }

                // 取消订阅并清理编辑期装饰管理器
                MapDecoration.DecorationDragRequested -= OnDecorationDragRequested;
                EndDrag(true); // 强制取消未完成的拖拽
                if (_editDecorationManager != null && IsInstanceValid(_editDecorationManager))
                {
                    UIInputPolicy.Instance?.UnregisterUiNode(_editDecorationManager);
                    _editDecorationManager.QueueFree();
                    _editDecorationManager = null;
                }

                UIInputPolicy.Instance?.UnregisterUiNode(_editGridManager);
                _editGridManager.QueueFree();
                _editGridManager = null;
            }

            // 1. 恢复游戏树运行，并恢复 UIInputPolicy 管理的 ProcessMode
            UIInputPolicy.Instance?.ResumeGame();
            UIInputPolicy.Instance?.UnregisterUiNode(this);
            if (Camera is Node camNode)
                UIInputPolicy.Instance?.UnregisterUiNode(camNode);

            // 2. 恢复玩家、所有怪物实例、所有NPC实例、巡逻覆盖层显示
            if (Player != null)
                Player.Visible = _wasPlayerVisible;

            var monsterMgr = GetTree().GetFirstNodeInGroup("monster_manager") as MonsterManager;
            monsterMgr?.SetAllMonstersVisible(true);

            var npcMgr = GetTree().GetFirstNodeInGroup("npc_manager") as NpcManager;
            npcMgr?.SetAllNpcsVisible(true);

            var patrolOverlay = GetTree().GetFirstNodeInGroup("monster_patrol_overlay") as MonsterPatrolOverlay;
            patrolOverlay?.SetOverlayEnabled(_wasMonsterPatrolOverlayVisible);

            var chestMgr = GetTree().GetFirstNodeInGroup("chest_manager") as ChestManager;
            chestMgr?.SetAllChestsVisible(true);

            var dropMgr = GetTree().GetFirstNodeInGroup("drop_manager") as DropManager;
            dropMgr?.SetAllDropsVisible(true);

            var decMgr = GetTree().GetFirstNodeInGroup("map_decoration_manager") as MapDecorationManager;
            decMgr?.SetAllDecorationsVisible(true);

            // 恢复所有与地图编辑器无关的 UI
            var funcBar = GetTree()?.GetFirstNodeInGroup("function_bar") as CanvasItem;
            if (funcBar != null)
                funcBar.Visible = _wasFunctionBarVisible;

            var skillBar = GetTree()?.GetFirstNodeInGroup("skill_bar") as CanvasItem;
            if (skillBar != null)
                skillBar.Visible = _wasSkillBarVisible;

            foreach (var panel in _wasVisiblePanels)
            {
                if (panel != null && IsInstanceValid(panel))
                    panel.Visible = true;
            }
            _wasVisiblePanels.Clear();

            // 3. 显示游戏 GridManager 并重新加载最新地图数据（反映编辑保存的结果）
            if (_gameGridManager != null)
            {
                _gameGridManager.Visible = true;
                _gameGridManager.LoadMap(_gameGridManager.CurrentMapName);
                GD.Print($"[MapEditor] 游戏 GridManager 重新加载地图: {_gameGridManager.CurrentMapName}");
            }

            // 4. 清除选中的格子
            SelectedCells.Clear();
            IsSelecting = false;
            QueueRedraw();

            HideEditorUi();
        }

        private void CreateEditorPanel()
        {
            var canvasLayer = new CanvasLayer();
            canvasLayer.Layer = 10;
            AddChild(canvasLayer);

            _editorPanel = new Control();
            _editorPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _editorPanel.Size = new Vector2(280, 620);
            _editorPanel.Position = new Vector2(-300, 10);
            canvasLayer.AddChild(_editorPanel);

            var panel = new Panel();
            panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _editorPanel.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.Name = "VBoxContainer";
            vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 3);
            _editorPanel.AddChild(vbox);

            // 标题
            var title = new Label();
            title.Name = "EditorTitle";
            title.Text = $"🗺️ 地图编辑器 - {GridManager?.CurrentMapName ?? "--"}";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(title);

            // 版本号诊断信息
            var diagLabel = new Label();
            diagLabel.Name = "DiagLabel";
            diagLabel.Text = $"[修复版本 {GridFixVersion}]";
            diagLabel.AddThemeColorOverride("font_color", Colors.Yellow);
            diagLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(diagLabel);

            vbox.AddChild(new HSeparator());

            // === 地图管理 ===
            var mapSelectHbox = new HBoxContainer();
            mapSelectHbox.Name = "MapSelectHbox";
            vbox.AddChild(mapSelectHbox);

            var mapOption = new OptionButton();
            mapOption.Name = "MapOption";
            mapOption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mapOption.ItemSelected += OnMapOptionSelected;
            mapSelectHbox.AddChild(mapOption);

            var switchBtn = new Button();
            switchBtn.Text = "切换";
            switchBtn.Pressed += OnSwitchMap;
            mapSelectHbox.AddChild(switchBtn);

            var mapActionHbox = new HBoxContainer();
            vbox.AddChild(mapActionHbox);

            var createMapBtn = new Button();
            createMapBtn.Text = "新建";
            createMapBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            createMapBtn.Pressed += OnCreateNewMapClicked;
            mapActionHbox.AddChild(createMapBtn);

            var deleteMapBtn = new Button();
            deleteMapBtn.Text = "删除";
            deleteMapBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            deleteMapBtn.Pressed += OnDeleteMapClicked;
            mapActionHbox.AddChild(deleteMapBtn);

            var renameMapBtn = new Button();
            renameMapBtn.Text = "重命名";
            renameMapBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            renameMapBtn.Pressed += OnRenameMapClicked;
            mapActionHbox.AddChild(renameMapBtn);

            vbox.AddChild(new HSeparator());

            // === 工具切换 ===
            var toolToggleRow = new HBoxContainer();
            vbox.AddChild(toolToggleRow);

            _terrainToolToggle = new Button();
            _terrainToolToggle.Text = "刷地形";
            _terrainToolToggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _terrainToolToggle.ToggleMode = true;
            _terrainToolToggle.Pressed += () => OnToolToggled(EditorTool.PaintTerrain);
            toolToggleRow.AddChild(_terrainToolToggle);

            _buildingToolToggle = new Button();
            _buildingToolToggle.Text = "放建筑";
            _buildingToolToggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _buildingToolToggle.ToggleMode = true;
            _buildingToolToggle.Pressed += () => OnToolToggled(EditorTool.PlaceDecoration);
            toolToggleRow.AddChild(_buildingToolToggle);

            // === 动态工具内容区 ===
            _toolContentContainer = new VBoxContainer();
            _toolContentContainer.Name = "ToolContentContainer";
            _toolContentContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vbox.AddChild(_toolContentContainer);

            vbox.AddChild(new HSeparator());

            // === 撤销/重做 ===
            var actionHbox = new HBoxContainer();
            vbox.AddChild(actionHbox);

            var undoBtn = new Button();
            undoBtn.Text = "↩ 撤销";
            undoBtn.CustomMinimumSize = new Vector2(0, 26);
            undoBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            undoBtn.Pressed += Undo;
            actionHbox.AddChild(undoBtn);

            var redoBtn = new Button();
            redoBtn.Name = "RedoBtn";
            redoBtn.Text = "↪ 重做";
            redoBtn.CustomMinimumSize = new Vector2(0, 26);
            redoBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            redoBtn.Pressed += Redo;
            actionHbox.AddChild(redoBtn);

            vbox.AddChild(new HSeparator());

            // === 显示设置 ===
            var showUidToggle = new CheckButton();
            showUidToggle.Name = "ShowUidToggle";
            showUidToggle.Text = "显示格子UID";
            showUidToggle.ButtonPressed = _gameGridManager?.ShowCellUids ?? false;
            showUidToggle.Toggled += OnShowUidToggled;
            vbox.AddChild(showUidToggle);

            vbox.AddChild(new HSeparator());

            // === 文件操作 ===
            var fileHbox = new HBoxContainer();
            vbox.AddChild(fileHbox);

            var saveBtn = new Button();
            saveBtn.Text = "💾 保存";
            saveBtn.CustomMinimumSize = new Vector2(0, 26);
            saveBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            saveBtn.Pressed += OnSavePressed;
            fileHbox.AddChild(saveBtn);

            var exportBtn = new Button();
            exportBtn.Text = "📤 导出";
            exportBtn.CustomMinimumSize = new Vector2(0, 26);
            exportBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            exportBtn.Pressed += OnExportPressed;
            fileHbox.AddChild(exportBtn);

            var importBtn = new Button();
            importBtn.Text = "📥 导入";
            importBtn.CustomMinimumSize = new Vector2(0, 26);
            importBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            importBtn.Pressed += OnImportPressed;
            fileHbox.AddChild(importBtn);

            // 退出按钮
            var exitBtn = new Button();
            exitBtn.Text = "✕ 退出编辑 (E)";
            exitBtn.CustomMinimumSize = new Vector2(0, 26);
            exitBtn.Pressed += () => SetEditMode(false);
            vbox.AddChild(exitBtn);

            // 提示
            var hint = new Label();
            hint.Text = "提示: 刷地形工具下左键点/拖直接刷，框选模式需先框选再应用；放建筑工具下从列表按住房舍拖到地图放置，拖动已放置建筑换位，Delete/删除按钮移除悬停建筑；右键地图外格子可开辟地图";
            hint.AddThemeColorOverride("font_color", Colors.Gray);
            hint.AddThemeFontSizeOverride("font_size", 10);
            hint.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(hint);

            RefreshToolContent();
            _editorPanel.Visible = false;

            // 编辑器面板由 UIInputPolicy 统一管理，确保游戏暂停时仍可交互
            UIInputPolicy.Instance?.RegisterUiNode(_editorPanel);
        }

        private Button? _terrainToolToggle;
        private Button? _buildingToolToggle;
        private VBoxContainer? _toolContentContainer;
        private bool _isRefreshingToolContent;

        private void OnToolToggled(EditorTool tool)
        {
            if (CurrentTool == tool) return;
            CurrentTool = tool;
            RefreshToolContent();
        }

        internal void RefreshToolContent()
        {
            if (_toolContentContainer == null || _isRefreshingToolContent) return;
            _isRefreshingToolContent = true;
            try
            {
                foreach (var child in _toolContentContainer.GetChildren())
                    child.QueueFree();
                _toolContentContainer.QueueSort();

                _terrainToolToggle?.SetPressedNoSignal(CurrentTool == EditorTool.PaintTerrain);
                _buildingToolToggle?.SetPressedNoSignal(CurrentTool == EditorTool.PlaceDecoration);

                if (CurrentTool == EditorTool.PaintTerrain)
                    CreateTerrainToolContent(_toolContentContainer);
                else
                    CreateBuildingToolContent(_toolContentContainer);
            }
            finally
            {
                _isRefreshingToolContent = false;
            }
        }

        private void CreateTerrainToolContent(VBoxContainer parent)
        {
            // 地形选择
            var terrainRow = new HBoxContainer();
            terrainRow.Name = "TerrainRow";
            parent.AddChild(terrainRow);

            var terrainLabel = new Label();
            terrainLabel.Text = "地形:";
            terrainLabel.AddThemeFontSizeOverride("font_size", 12);
            terrainLabel.CustomMinimumSize = new Vector2(40, 0);
            terrainRow.AddChild(terrainLabel);

            var terrainOption = new OptionButton();
            terrainOption.Name = "TerrainOption";
            terrainOption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            foreach (var kvp in TerrainConfigUtil.Configs)
                terrainOption.AddItem(kvp.Value.Name, kvp.Key);
            terrainOption.ItemSelected += OnTerrainSelected;
            for (int i = 0; i < terrainOption.ItemCount; i++)
            {
                if (terrainOption.GetItemId(i) == PaintTerrain)
                {
                    terrainOption.Select(i);
                    break;
                }
            }
            terrainRow.AddChild(terrainOption);

            // 模式切换
            var modeRow = new HBoxContainer();
            parent.AddChild(modeRow);

            var modeLabel = new Label();
            modeLabel.Text = "模式:";
            modeLabel.AddThemeFontSizeOverride("font_size", 12);
            modeLabel.CustomMinimumSize = new Vector2(40, 0);
            modeRow.AddChild(modeLabel);

            var brushBtn = new Button();
            brushBtn.Text = "笔刷";
            brushBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            brushBtn.ToggleMode = true;
            brushBtn.ButtonPressed = CurrentTerrainPaintMode == TerrainPaintMode.Brush;
            brushBtn.Pressed += () => OnTerrainPaintModeChanged(TerrainPaintMode.Brush);
            modeRow.AddChild(brushBtn);

            var boxBtn = new Button();
            boxBtn.Text = "框选";
            boxBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            boxBtn.ToggleMode = true;
            boxBtn.ButtonPressed = CurrentTerrainPaintMode == TerrainPaintMode.BoxSelect;
            boxBtn.Pressed += () => OnTerrainPaintModeChanged(TerrainPaintMode.BoxSelect);
            modeRow.AddChild(boxBtn);

            // 应用按钮（只在框选模式显示）
            var applyBtn = new Button();
            applyBtn.Name = "TerrainApplyBtn";
            applyBtn.Text = "✓ 应用到选中";
            applyBtn.CustomMinimumSize = new Vector2(0, 26);
            applyBtn.Visible = CurrentTerrainPaintMode == TerrainPaintMode.BoxSelect;
            applyBtn.Pressed += ApplyToSelection;
            parent.AddChild(applyBtn);
        }

        private void CreateBuildingToolContent(VBoxContainer parent)
        {
            // 搜索 + 删除
            var headerRow = new HBoxContainer();
            headerRow.Name = "BuildingHeaderRow";
            parent.AddChild(headerRow);

            var searchEdit = new LineEdit();
            searchEdit.Name = "BuildingSearchEdit";
            searchEdit.PlaceholderText = "搜索建筑...";
            searchEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            searchEdit.TextChanged += OnBuildingSearchTextChanged;
            headerRow.AddChild(searchEdit);

            var deleteDecorBtn = new Button();
            deleteDecorBtn.Text = "删除";
            deleteDecorBtn.CustomMinimumSize = new Vector2(50, 26);
            deleteDecorBtn.Pressed += DeleteSelectedDecorations;
            headerRow.AddChild(deleteDecorBtn);

            var openWorkshopBtn = new Button();
            openWorkshopBtn.Text = "建筑工坊";
            openWorkshopBtn.CustomMinimumSize = new Vector2(70, 26);
            openWorkshopBtn.Pressed += OnOpenDecorationWorkshop;
            headerRow.AddChild(openWorkshopBtn);

            // 建筑列表
            var scroll = new ScrollContainer();
            scroll.Name = "BuildingListScroll";
            scroll.CustomMinimumSize = new Vector2(0, 180);
            scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            parent.AddChild(scroll);

            var listBox = new VBoxContainer();
            listBox.Name = "BuildingListBox";
            listBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(listBox);

            // 注意：不要在这里调用 DecorationConfigUtil.RefreshFromProfileManager()，
            // 因为它会触发 ProfilesChanged -> OnDecorationProfilesChanged -> RefreshToolContent，
            // 形成无限循环。Configs 在 EntityProfileManager 加载时已同步，后续外部变更
            // 通过 ProfilesChanged 事件自动刷新。
            var decorationConfigs = new System.Collections.Generic.List<DecorationConfig>();
            foreach (var kvp in DecorationConfigUtil.Configs)
            {
                if (kvp.Key != 0)
                    decorationConfigs.Add(kvp.Value);
            }

            GD.Print($"[MapEditor] 创建建筑列表，共 {decorationConfigs.Count} 个配置");

            foreach (var cfg in decorationConfigs)
            {
                var item = new BuildingListItem(this, cfg);
                item.Name = $"BuildingItem_{cfg.Id}";
                listBox.AddChild(item);
            }
        }

        private void OnTerrainPaintModeChanged(TerrainPaintMode mode)
        {
            CurrentTerrainPaintMode = mode;
            SelectedCells.Clear();
            IsSelecting = false;
            QueueRedraw();
            RefreshToolContent();
        }

        private void ShowEditorUi()
        {
            if (_editorPanel == null)
                CreateEditorPanel();
            if (_editorPanel != null)
            {
                _editorPanel.Visible = true;
                _editorPanel.MouseFilter = Control.MouseFilterEnum.Stop;

                // 编辑器 UI 已初始化
            }
        }

        private void HideEditorUi()
        {
            if (_editorPanel != null)
            {
                _editorPanel.Visible = false;
                _editorPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
            }
        }



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
            _selectionBeforeDrag = new System.Collections.Generic.Dictionary<Vector2I, bool>(SelectedCells);

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
                    NewSelection = new System.Collections.Generic.Dictionary<Vector2I, bool>(SelectedCells)
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
        private System.Collections.Generic.List<Vector2I> GetFootprintCells(Vector2I anchor, int sizeX, int sizeY)
        {
            var cells = new System.Collections.Generic.List<Vector2I>();
            sizeX = Mathf.Max(1, sizeX);
            sizeY = Mathf.Max(1, sizeY);
            for (int dy = 0; dy < sizeY; dy++)
                for (int dx = 0; dx < sizeX; dx++)
                    cells.Add(new Vector2I(anchor.X + dx, anchor.Y + dy));
            return cells;
        }

        /// <summary>获取指定锚点建筑的占地格子集合（用于移动时排除自身）</summary>
        private System.Collections.Generic.HashSet<Vector2I> GetOccupiedFootprintCells(Vector2I anchor, int sizeX, int sizeY)
        {
            var set = new System.Collections.Generic.HashSet<Vector2I>();
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

        private bool IsMouseOverEditorPanel()
        {
            if (_editorPanel == null || !_editorPanel.Visible)
                return false;
            var mousePos = GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            return _editorPanel.GetGlobalRect().HasPoint(mousePos);
        }

        private bool SelectionEquals(System.Collections.Generic.Dictionary<Vector2I, bool> a, System.Collections.Generic.Dictionary<Vector2I, bool> b)
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

            var newSelection = new System.Collections.Generic.Dictionary<Vector2I, bool>();
            foreach (var pos in GridManager.GridData.Keys)
            {
                if (!SelectedCells.ContainsKey(pos))
                    newSelection[pos] = true;
            }

            SelectedCells = newSelection;
            UpdateSelectionLabel();
            QueueRedraw();
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
        /// 创建鼠标悬停提示框（CanvasLayer + PanelContainer + Label）
        /// </summary>
        private void EnsureHoverTooltip()
        {
            if (_hoverTooltipCanvasLayer != null) return;

            _hoverTooltipCanvasLayer = new CanvasLayer();
            _hoverTooltipCanvasLayer.Layer = 11;
            AddChild(_hoverTooltipCanvasLayer);

            _hoverTooltipPanel = new PanelContainer();
            _hoverTooltipPanel.Name = "HoverTooltipPanel";
            _hoverTooltipPanel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _hoverTooltipPanel.Visible = false;
            _hoverTooltipPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
            _hoverTooltipPanel.CustomMinimumSize = new Vector2(_hoverTooltipWidth, 0);
            _hoverTooltipCanvasLayer.AddChild(_hoverTooltipPanel);

            var styleBox = new StyleBoxFlat
            {
                BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                BorderColor = new Color(0.4f, 0.4f, 0.4f, 0.9f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusBottomLeft = 4,
                ContentMarginLeft = 8,
                ContentMarginTop = 6,
                ContentMarginRight = 8,
                ContentMarginBottom = 6,
            };
            _hoverTooltipPanel.AddThemeStyleboxOverride("panel", styleBox);

            _hoverTooltipLabel = new Label();
            _hoverTooltipLabel.Name = "HoverTooltipLabel";
            _hoverTooltipLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            _hoverTooltipLabel.AddThemeFontSizeOverride("font_size", 12);
            _hoverTooltipLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.95f, 1.0f));
            _hoverTooltipLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            _hoverTooltipLabel.CustomMinimumSize = new Vector2(_hoverTooltipWidth, 0);
            _hoverTooltipPanel.AddChild(_hoverTooltipLabel);
        }

        /// <summary>
        /// 让悬停提示框跟随鼠标，并在靠近屏幕边缘时自动调整位置避免截断
        /// </summary>
        private void UpdateHoverTooltipPosition()
        {
            if (_hoverTooltipPanel == null || !_hoverTooltipPanel.Visible)
                return;

            var viewport = GetViewport();
            if (viewport == null) return;

            var mousePos = viewport.GetMousePosition();
            var offset = new Vector2(14, 14);
            var pos = mousePos + offset;

            var viewportSize = viewport.GetVisibleRect().Size;
            var size = _hoverTooltipPanel.Size;

            // 右边缘超出时，显示在鼠标左侧
            if (pos.X + size.X > viewportSize.X)
                pos.X = mousePos.X - size.X - 6;
            // 下边缘超出时，显示在鼠标上方
            if (pos.Y + size.Y > viewportSize.Y)
                pos.Y = mousePos.Y - size.Y - 6;

            _hoverTooltipPanel.Position = pos;
        }

        /// <summary>
        /// 更新鼠标悬停提示框的内容和显隐
        /// </summary>
        private void UpdateHoverInfo()
        {
            EnsureHoverTooltip();

            var grid = GridManager;
            if (grid == null || _hoveredGridPos == new Vector2I(-1, -1) || !grid.IsInBounds(_hoveredGridPos))
            {
                _hoverTooltipPanel.Visible = false;
                return;
            }

            var cell = grid.GetCell(_hoveredGridPos);
            if (cell == null)
            {
                _hoverTooltipPanel.Visible = false;
                return;
            }

            var terrainName = cell.GetTerrainName();
            var walkable = cell.TerrainConfig?.Walkable ?? true;
            var walkableStr = walkable ? "✓ 可行走" : "✗ 不可行走";
            var decorationStr = cell.DecorationType != 0
                ? $"\n  建筑: {DecorationConfigUtil.GetDisplayName(cell.DecorationType)} ({cell.DecorationType})"
                : "";

            _hoverTooltipLabel.Text = $"鼠标悬停: ({cell.Pos.X},{cell.Pos.Y})\n" +
                                      $"  UID: {cell.Uid}\n" +
                                      $"  地形: {terrainName} ({cell.TerrainType})\n" +
                                      $"  高度: {cell.Height}" +
                                      decorationStr +
                                      $"\n  {walkableStr}";
            _hoverTooltipPanel.Visible = true;
            UpdateHoverTooltipPosition();
        }

        // ============ 属性应用 ============

        private void ApplyToSelection()
        {
            if (SelectedCells.Count == 0 || GridManager == null)
                return;

            // 在修改前记录撤销命令
            var cmd = CreateUndoCommand(PaintTerrain);
            if (cmd.Changes.Count == 0) return;

            _undoStack.Add(cmd);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);
            _redoStack.Clear(); // 新操作后清空重做栈

            foreach (var (pos, oldTerrain) in cmd.Changes)
            {
                var cell = GridManager.GetCell(pos);
                if (cell == null) continue;
                cell.TerrainType = PaintTerrain;
                cell.TerrainConfig = TerrainConfigUtil.Get(PaintTerrain);
                GD.Print($"[MapEditor.Apply] pos=({pos.X},{pos.Y}) old={oldTerrain} new={PaintTerrain} walkable={cell.TerrainConfig?.Walkable ?? true}");
            }

            GridManager.NotifyTerrainChanged();
            GD.Print("[MapEditor] 已应用地形到 " + cmd.Changes.Count + " 个格子");
        }

        /// <summary>
        /// 创建装饰撤销命令 — 记录当前选中格子中将被修改的格子及其旧装饰值。
        /// </summary>
        private DecorationEditCommand CreateDecorationUndoCommand(int newDecorationType)
        {
            var cmd = new DecorationEditCommand();
            if (GridManager == null) return cmd;

            foreach (var pos in SelectedCells.Keys)
            {
                if (!GridManager.IsInBounds(pos)) continue;
                var cell = GridManager.GetCell(pos);
                if (cell == null) continue;
                if (cell.DecorationType == newDecorationType) continue;
                cmd.Changes.Add((pos, cell.DecorationType, newDecorationType));
            }
            return cmd;
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


        // ============ 地形笔刷 ============

        private void BrushTerrainAtMouse()
        {
            if (_editGridManager == null) return;
            var mouseLocalPos = _editGridManager.ToLocal(GetGlobalMousePosition());
            var gridPos = _editGridManager.WorldToGrid(mouseLocalPos);
            if (!_editGridManager.IsInBounds(gridPos)) return;
            if (gridPos == _lastBrushedGridPos) return;

            _lastBrushedGridPos = gridPos;
            var cell = _editGridManager.GetCell(gridPos);
            if (cell == null || cell.TerrainType == PaintTerrain) return;

            var cmd = new TerrainEditCommand { NewTerrainType = PaintTerrain };
            cmd.Changes.Add((gridPos, cell.TerrainType));
            _undoStack.Add(cmd);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();

            cell.TerrainType = PaintTerrain;
            cell.TerrainConfig = TerrainConfigUtil.Get(PaintTerrain);
            _editGridManager.NotifyTerrainChanged();
            GD.Print($"[MapEditor.Brush] pos={gridPos} terrain={PaintTerrain}");
        }

        // ============ UI回调 ============

        private void OnTerrainSelected(long index)
        {
            var terrainOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/ToolContentContainer/TerrainRow/TerrainOption");
            if (terrainOption != null)
            {
                PaintTerrain = terrainOption.GetItemId((int)index);
                GD.Print($"[MapEditor] 选择地形: index={index}, id={PaintTerrain}, name={terrainOption.GetItemText((int)index)}");
            }
            else
            {
                PaintTerrain = (int)index;
            }
        }

        private void OnShowUidToggled(bool pressed)
        {
            var grid = GridManager;
            if (grid == null) return;
            grid.SetShowCellUids(pressed);
            GD.Print($"[MapEditor] 显示格子UID: {pressed}");
        }

        private void OnBuildingSearchTextChanged(string text)
        {
            var listBox = _editorPanel?.GetNodeOrNull<VBoxContainer>("VBoxContainer/ToolContentContainer/BuildingListScroll/BuildingListBox");
            if (listBox == null) return;

            var filter = text?.Trim().ToLowerInvariant() ?? "";
            foreach (var child in listBox.GetChildren())
            {
                if (child is not BuildingListItem item) continue;
                var cfg = item.Config;
                var match = string.IsNullOrEmpty(filter)
                    || cfg.DisplayName.ToLowerInvariant().Contains(filter)
                    || cfg.Name.ToLowerInvariant().Contains(filter)
                    || (!string.IsNullOrEmpty(cfg.PinyinName) && cfg.PinyinName.ToLowerInvariant().Contains(filter));
                item.Visible = match;
            }

            listBox.QueueSort();
        }

        private void OnDecorationProfilesChanged()
        {
            if (IsEditing && CurrentTool == EditorTool.PlaceDecoration)
                RefreshToolContent();
        }

        private void OnOpenDecorationWorkshop()
        {
            var debugPanel = GetTree()?.GetFirstNodeInGroup("debug_panel") as DebugPanel;
            if (debugPanel == null)
            {
                ShowToast("未找到调试面板", Colors.Yellow);
                return;
            }
            debugPanel.SelectDecorationTab();
        }

        /// <summary>进入地图编辑模式并切换到放建筑工具</summary>
        public void EnterPlaceDecorationMode()
        {
            SetEditMode(true);
            CurrentTool = EditorTool.PlaceDecoration;
            RefreshToolContent();
        }

        // ============ 选择历史系统（右键撤销） ============

        private void SaveSelectionHistory()
        {
            // 保存当前选择状态到历史栈
            var historyCopy = new System.Collections.Generic.Dictionary<Vector2I, bool>();
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
        private System.Collections.Generic.Dictionary<Vector2I, GridCell> DeepCopyGridData(System.Collections.Generic.Dictionary<Vector2I, GridCell> source)
        {
            var copy = new System.Collections.Generic.Dictionary<Vector2I, GridCell>();
            foreach (var kvp in source)
            {
                var cell = kvp.Value;
                var newCell = new GridCell(cell.Pos.X, cell.Pos.Y);
                newCell.Uid = cell.Uid;
                newCell.TerrainType = cell.TerrainType;
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

        // ============ 撤销系统 ============

        /// <summary>
        /// 创建撤销命令 — 记录当前选中格子中将被修改的格子及其旧地形值。
        /// 必须在修改格子之前调用。
        /// </summary>
        private TerrainEditCommand CreateUndoCommand(int newTerrainType)
        {
            var cmd = new TerrainEditCommand { NewTerrainType = newTerrainType };
            if (GridManager == null) return cmd;

            foreach (var pos in SelectedCells.Keys)
            {
                if (!GridManager.IsInBounds(pos)) continue;
                var cell = GridManager.GetCell(pos);
                if (cell == null) continue;
                // 跳过没有实际变化的格子，避免撤销栈被无意义命令占满
                if (cell.TerrainType == newTerrainType) continue;
                cmd.Changes.Add((pos, cell.TerrainType));
            }
            return cmd;
        }

        private void Undo()
        {
            if (_undoStack.Count == 0 || GridManager == null)
            {
                GD.Print("[MapEditor] 没有可撤销的操作");
                return;
            }

            var cmd = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            GD.Print($"[MapEditor.Undo] 撤销命令类型={cmd.GetType().Name}, 剩余undo={_undoStack.Count}");
            cmd.Undo(GridManager);
            if (cmd is not SelectionEditCommand)
                SelectedCells.Clear();
            QueueRedraw();

            _redoStack.Add(cmd);
            if (_redoStack.Count > MaxUndoSteps)
                _redoStack.RemoveAt(0);

            GD.Print("[MapEditor] 撤销操作完成");
        }

        private void Redo()
        {
            if (_redoStack.Count == 0 || GridManager == null)
            {
                GD.Print("[MapEditor] 没有可重做的操作");
                return;
            }

            var cmd = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            GD.Print($"[MapEditor.Redo] 重做命令类型={cmd.GetType().Name}");
            cmd.Redo(GridManager);
            if (cmd is not SelectionEditCommand)
                SelectedCells.Clear();
            QueueRedraw();

            _undoStack.Add(cmd);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);

            GD.Print("[MapEditor] 重做操作完成");
        }

        // ============ 文件操作 ============

        private void OnSavePressed()
        {
            if (GridManager == null)
                return;

            var mapName = GridManager.CurrentMapName;
            var confirmDialog = new ConfirmationDialog();
            confirmDialog.Title = "保存地图";
            confirmDialog.DialogText = $"确定要保存地图 '{mapName}' 吗？\n这将覆盖现有的地图文件。";
            confirmDialog.Confirmed += () =>
            {
                var err = GridManager.SaveCurrentMap();
                if (err == Error.Ok)
                {
                    GD.Print($"[MapEditor] 地图 '{mapName}' 保存成功");
                    ShowToast($"地图 '{mapName}' 保存成功!");
                }
                else
                {
                    GD.PushError($"[MapEditor] 地图 '{mapName}' 保存失败: " + err);
                    ShowToast($"保存失败: {err}", Colors.Red);
                }
                confirmDialog.QueueFree();
            };
            confirmDialog.Canceled += () => confirmDialog.QueueFree();
            AddChild(confirmDialog);
            confirmDialog.PopupCentered();
        }

        private void ShowToast(string message, Color? color = null)
        {
            var toast = new Label();
            toast.Text = message;
            toast.HorizontalAlignment = HorizontalAlignment.Center;
            toast.AddThemeColorOverride("font_color", color ?? Colors.Green);
            toast.AddThemeFontSizeOverride("font_size", 16);

            var panel = new Panel();
            panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            panel.Position = new Vector2(0, 60);
            panel.AddChild(toast);

            var canvasLayer = new CanvasLayer();
            canvasLayer.Layer = 100;
            canvasLayer.AddChild(panel);
            AddChild(canvasLayer);

            // 2秒后自动消失
            var timer = new Timer();
            timer.WaitTime = 2.0;
            timer.OneShot = true;
            timer.Timeout += () =>
            {
                canvasLayer.QueueFree();
            };
            canvasLayer.AddChild(timer);
            timer.Start();
        }

        private void OnExportPressed()
        {
            var dialog = new FileDialog();
            dialog.FileMode = FileDialog.FileModeEnum.SaveFile;
            dialog.Access = FileDialog.AccessEnum.Filesystem;
            dialog.Filters = new[] { "*.json" };
            dialog.CurrentFile = GridManager.CurrentMapName + ".json";
            dialog.FileSelected += (path) =>
            {
                DoExport(path);
                dialog.QueueFree();
            };
            dialog.Canceled += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(800, 600));
        }

        private void DoExport(string path)
        {
            if (GridManager == null)
                return;
            var err = GridManager.ExportJson(path);
            if (err == Error.Ok)
                GD.Print("[MapEditor] 导出成功: " + path);
            else
                GD.PushError("[MapEditor] 导出失败: " + err);
        }

        private void OnImportPressed()
        {
            var dialog = new FileDialog();
            dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
            dialog.Access = FileDialog.AccessEnum.Filesystem;
            dialog.Filters = new[] { "*.json" };
            dialog.FileSelected += (path) =>
            {
                DoImport(path);
                dialog.QueueFree();
            };
            dialog.Canceled += () => dialog.QueueFree();
            dialog.FileSelected += DoImport;
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(800, 600));
        }

        private void DoImport(string path)
        {
            if (GridManager == null)
                return;
            var err = GridManager.ImportJson(path);
            if (err == Error.Ok)
                GD.Print("[MapEditor] 导入成功: " + path);
            else
                GD.PushError("[MapEditor] 导入失败: " + err);
        }

        // ============ 地图管理 ============

        private void RefreshMapList()
        {
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption == null) return;

            mapOption.Clear();
            var maps = MapDataManager.GetMapList();
            maps.Sort();

            int selectedIndex = 0;
            for (int i = 0; i < maps.Count; i++)
            {
                mapOption.AddItem(maps[i], i);
                if (GridManager != null && maps[i] == GridManager.CurrentMapName)
                    selectedIndex = i;
            }

            if (maps.Count > 0)
                mapOption.Select(selectedIndex);
        }

        private void OnSwitchMap()
        {
            DoSwitchToSelectedMap();
        }

        private void OnMapOptionSelected(long index)
        {
            DoSwitchToSelectedMap();
        }

        private void DoSwitchToSelectedMap()
        {
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption == null || GridManager == null) return;

            var selectedName = mapOption.GetItemText(mapOption.Selected);
            if (string.IsNullOrEmpty(selectedName) || selectedName == GridManager.CurrentMapName)
                return;

            SwitchToMap(selectedName);
        }

        private void SwitchToMap(string mapName)
        {
            if (GridManager == null) return;

            // 诊断：检查游戏 GridManager 是否被意外显示
            GD.Print($"[MapEditor] SwitchToMap: 请求切换 to '{mapName}', 当前='{GridManager.CurrentMapName}'");
            GD.Print($"[MapEditor] SwitchToMap: _gameGridManager.Visible={_gameGridManager?.Visible}, _editGridManager.Visible={_editGridManager?.Visible}");

            // 诊断：检查场景中所有 GridManager 实例
            var allGrids = GetTree()?.GetNodesInGroup("grid_manager");
            GD.Print($"[MapEditor] SwitchToMap: 场景中 grid_manager 数量={allGrids?.Count ?? 0}");
            if (allGrids != null)
            {
                foreach (var n in allGrids)
                {
                    if (n is GridManager gm)
                        GD.Print($"[MapEditor] SwitchToMap:   GridManager '{gm.Name}' Visible={gm.Visible} Map={gm.CurrentMapName} Pos={gm.GlobalPosition}");
                }
            }

            if (!GridManager.LoadMap(mapName))
            {
                ShowToast($"加载地图 '{mapName}' 失败", Colors.Red);
                GD.PrintErr($"[MapEditor] SwitchToMap: LoadMap('{mapName}') 返回 false, 未切换");
                return;
            }
            GD.Print($"[MapEditor] SwitchToMap: LoadMap 成功, GridData={GridManager.MapWidth}x{GridManager.MapHeight}");

            // 将相机移动到新地图中心，确保新地图在视野内
            if (Camera != null && GridManager != null)
            {
                var bounds = GridManager.MapBounds;
                var mapCenter = GridManager.GlobalPosition +
                                new Vector2(
                                    (bounds.Position.X + bounds.Size.X / 2.0f) * GridManager.GridSize,
                                    (bounds.Position.Y + bounds.Size.Y / 2.0f) * GridManager.GridSize);
                Camera.GlobalPosition = mapCenter;
                GD.Print($"[MapEditor] SwitchToMap: 相机移动到新地图中心 {mapCenter}");
            }

            // 清空编辑状态
            SelectedCells.Clear();
            IsSelecting = false;
            _undoStack.Clear();
            _redoStack.Clear();
            _selectionHistory.Clear();

            // 更新面板显示
            var title = _editorPanel?.GetNodeOrNull<Label>("VBoxContainer/EditorTitle");
            if (title != null)
                title.Text = $"🗺️ 地图编辑器 - {GridManager.CurrentMapName}";

            // 同步下拉栏选中项
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption != null)
            {
                for (int i = 0; i < mapOption.ItemCount; i++)
                {
                    if (mapOption.GetItemText(i) == GridManager.CurrentMapName)
                    {
                        mapOption.Select(i);
                        break;
                    }
                }
            }

            QueueRedraw();
            ShowToast($"已切换到地图 '{mapName}'");
            GD.Print($"[MapEditor] 切换到地图: {mapName}");
        }

        private void OnCreateNewMapClicked()
        {
            ShowCreateMapDialog();
        }

        private void ShowCreateMapDialog()
        {
            var dialog = new AcceptDialog();
            dialog.Title = "新建地图";
            dialog.Size = new Vector2I(350, 200);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);

            // 名称
            var nameRow = new HBoxContainer();
            var nameLabel = new Label { Text = "地图名称:", CustomMinimumSize = new Vector2(80, 0) };
            var nameEdit = new LineEdit { Name = "NameEdit", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nameRow.AddChild(nameLabel);
            nameRow.AddChild(nameEdit);
            vbox.AddChild(nameRow);

            // 宽度
            var widthRow = new HBoxContainer();
            var widthLabel = new Label { Text = "宽度:", CustomMinimumSize = new Vector2(80, 0) };
            var widthSpin = new SpinBox { Name = "WidthSpin", MinValue = 10, MaxValue = 200, Value = 50, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            widthRow.AddChild(widthLabel);
            widthRow.AddChild(widthSpin);
            vbox.AddChild(widthRow);

            // 高度
            var heightRow = new HBoxContainer();
            var heightLabel = new Label { Text = "高度:", CustomMinimumSize = new Vector2(80, 0) };
            var heightSpin = new SpinBox { Name = "HeightSpin", MinValue = 10, MaxValue = 200, Value = 50, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            heightRow.AddChild(heightLabel);
            heightRow.AddChild(heightSpin);
            vbox.AddChild(heightRow);

            dialog.AddChild(vbox);

            dialog.Confirmed += () =>
            {
                var name = nameEdit.Text.StripEdges();
                if (string.IsNullOrEmpty(name))
                {
                    ShowToast("地图名称不能为空", Colors.Red);
                    return;
                }
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"[\\/:*?""<>|]"))
                {
                    ShowToast("地图名称包含非法字符", Colors.Red);
                    return;
                }
                if (MapDataManager.MapExists(name))
                {
                    ShowToast($"地图 '{name}' 已存在", Colors.Red);
                    return;
                }

                int width = (int)widthSpin.Value;
                int height = (int)heightSpin.Value;

                var err = MapDataManager.CreateNewMap(name, width, height);
                if (err == Error.Ok)
                {
                    ShowToast($"地图 '{name}' 创建成功");
                    RefreshMapList();
                    SwitchToMap(name);
                }
                else
                {
                    ShowToast($"创建失败: {err}", Colors.Red);
                }

                dialog.QueueFree();
            };

            dialog.Canceled += () => dialog.QueueFree();

            AddChild(dialog);
            dialog.PopupCentered();
        }

        private void OnDeleteMapClicked()
        {
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption == null) return;

            var selectedName = mapOption.GetItemText(mapOption.Selected);
            if (string.IsNullOrEmpty(selectedName))
                return;

            var maps = MapDataManager.GetMapList();
            if (maps.Count <= 1)
            {
                ShowToast("至少保留一张地图", Colors.Red);
                return;
            }

            var confirm = new ConfirmationDialog();
            confirm.Title = "删除地图";
            confirm.DialogText = $"确定要删除地图 '{selectedName}' 吗？\n此操作不可撤销！";
            confirm.Confirmed += () =>
            {
                var err = MapDataManager.DeleteMap(selectedName);
                if (err == Error.Ok)
                {
                    ShowToast($"地图 '{selectedName}' 已删除");
                    RefreshMapList();

                    // 若删除的是当前地图，切换到第一个可用地图
                    if (GridManager != null && selectedName == GridManager.CurrentMapName)
                    {
                        var remaining = MapDataManager.GetMapList();
                        if (remaining.Count > 0)
                            SwitchToMap(remaining[0]);
                    }
                }
                else
                {
                    ShowToast($"删除失败: {err}", Colors.Red);
                }
                confirm.QueueFree();
            };
            confirm.Canceled += () => confirm.QueueFree();
            AddChild(confirm);
            confirm.PopupCentered();
        }

        private void OnRenameMapClicked()
        {
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption == null) return;

            var selectedName = mapOption.GetItemText(mapOption.Selected);
            if (string.IsNullOrEmpty(selectedName))
                return;

            ShowRenameMapDialog(selectedName);
        }

        private void ShowRenameMapDialog(string oldName)
        {
            var dialog = new AcceptDialog();
            dialog.Title = "重命名地图";
            dialog.Size = new Vector2I(350, 150);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);

            var oldRow = new HBoxContainer();
            oldRow.AddChild(new Label { Text = "原名称:", CustomMinimumSize = new Vector2(80, 0) });
            oldRow.AddChild(new Label { Text = oldName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            vbox.AddChild(oldRow);

            var newRow = new HBoxContainer();
            newRow.AddChild(new Label { Text = "新名称:", CustomMinimumSize = new Vector2(80, 0) });
            var newEdit = new LineEdit { Name = "NewNameEdit", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            newRow.AddChild(newEdit);
            vbox.AddChild(newRow);

            dialog.AddChild(vbox);

            dialog.Confirmed += () =>
            {
                var newName = newEdit.Text.StripEdges();
                if (string.IsNullOrEmpty(newName))
                {
                    ShowToast("新名称不能为空", Colors.Red);
                    return;
                }
                if (System.Text.RegularExpressions.Regex.IsMatch(newName, @"[\\/:*?""<>|]"))
                {
                    ShowToast("名称包含非法字符", Colors.Red);
                    return;
                }
                if (MapDataManager.MapExists(newName))
                {
                    ShowToast($"地图 '{newName}' 已存在", Colors.Red);
                    return;
                }

                var err = MapDataManager.RenameMap(oldName, newName);
                if (err == Error.Ok)
                {
                    ShowToast($"重命名成功: {oldName} -> {newName}");
                    // 若重命名的是当前地图，同步更新 CurrentMapName
                    if (GridManager != null && oldName == GridManager.CurrentMapName)
                    {
                        GridManager.CurrentMapName = newName;
                    }
                    RefreshMapList();
                }
                else
                {
                    ShowToast($"重命名失败: {err}", Colors.Red);
                }
                dialog.QueueFree();
            };

            dialog.Canceled += () => dialog.QueueFree();

            AddChild(dialog);
            dialog.PopupCentered();
        }
    }
}
