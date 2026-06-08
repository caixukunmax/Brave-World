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
        public GridManager GridManager { get; private set; }
        public Camera2D Camera { get; private set; }
        public Node2D Player { get; private set; }

        // 编辑状态
        public bool IsEditing { get; private set; } = false;

        // 进入编辑模式前的游戏状态快照（用于退出时恢复）
        private bool _wasTreePaused;
        private bool _wasPlayerVisible;
        private bool _wasMonsterPatrolOverlayVisible;
        private bool _wasFunctionBarVisible;
        private bool _wasSkillBarVisible;
        private System.Collections.Generic.List<DraggablePanel> _wasVisiblePanels = new();

        // 选中系统
        public Dictionary<Vector2I, bool> SelectedCells { get; private set; } = new Dictionary<Vector2I, bool>();  // Vector2i -> bool
        public bool IsSelecting { get; private set; } = false;       // 是否正在拖拽选择
        public Vector2I SelectionStart { get; private set; }        // 框选起始格子
        public Vector2I SelectionEnd { get; private set; }          // 框选结束格子

        // 当前设置的属性（应用到选中格子）
        public int PaintTerrain { get; set; } = 0;

        // UI引用
        private Control _editorPanel;

        // 按键配置（可由调试面板设置）
        public bool RequireCtrlForSelection { get; set; } = true;  // 是否需要Ctrl键才能选中

        // 撤销历史（差分命令模式：只记录被修改格子的旧值，不再深拷贝全网格）
        private System.Collections.Generic.List<TerrainEditCommand> _undoStack = new();
        private System.Collections.Generic.List<TerrainEditCommand> _redoStack = new();
        public const int MaxUndoSteps = 20;

        /// <summary>
        /// 地形编辑命令 — 记录一次编辑操作中被修改格子的旧地形值。
        /// 仅存储 (位置, 旧值) 差分，而非完整网格深拷贝。
        /// </summary>
        private class TerrainEditCommand
        {
            /// <summary>应用此命令后的新地形值（用于 Redo）</summary>
            public int NewTerrainType;
            /// <summary>被修改的格子列表及其旧地形值</summary>
            public List<(Vector2I Pos, int OldTerrainType)> Changes = new();

            public void Undo(GridManager grid)
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

            public void Redo(GridManager grid)
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

        // 选择历史（用于右键撤销选择）
        private List<Dictionary<Vector2I, bool>> _selectionHistory = new List<Dictionary<Vector2I, bool>>();  // 每次选择操作前保存选中状态
        public const int MaxSelectionHistory = 10;

        public override async void _Ready()
        {
            // 循环等待关键节点出现（最多10帧），避免 _Ready 时机差异导致获取失败
            for (int i = 0; i < 10; i++)
            {
                GridManager = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
                Camera = GetTree()?.GetFirstNodeInGroup("camera") as Camera2D;
                Player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
                if (GridManager != null && Camera != null)
                    break;
                await ToSignal(GetTree(), "process_frame");
            }
            GD.Print($"[MapEditor] 初始化完成 GridManager={(GridManager != null ? "OK" : "NULL")} Camera={(Camera != null ? "OK" : "NULL")}");

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

        // 网格修复版本号，每次修改后递增，用于验证客户端加载的是最新代码
        public const string GridFixVersion = "v4.5"; // AA shader overlay with debug-tunable softness

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
                    // 编辑模式下实际线宽会被强制到至少 1.5px，诊断标签需要反映真实值
                    if (GridManager.IsEditMode && screenLineWidth < 1.5f)
                        screenLineWidth = 1.5f;
                    var mode = "整体-shader";
                    var actualColor = GridManager.IsEditMode ? new Color(1.0f, 1.0f, 1.0f, 1.0f) : lineColor;
                    var aaSoftness = GridManager.GetGridAntiAliasSoftness();
                    diagLabel.Text = $"[修复版本 {GridFixVersion}]\n" +
                                     $"zoom={zoom:F2} 实际线宽={screenLineWidth:F2}px 模式={mode}\n" +
                                     $"颜色=({actualColor.R:F2},{actualColor.G:F2},{actualColor.B:F2},{actualColor.A:F2}) 柔化={aaSoftness:F1}x";
                }
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

            // 撤销 (Ctrl+Z)
            if (Input.IsKeyPressed(Key.Ctrl) && Input.IsKeyPressed(Key.Z))
            {
                Undo();
                return;
            }

            // 全选 (Ctrl+A)
            if (Input.IsKeyPressed(Key.Ctrl) && Input.IsKeyPressed(Key.A))
            {
                SelectAll();
                return;
            }

            // 删除选中格子 (Delete键)
            if (Input.IsKeyPressed(Key.Delete))
            {
                DeleteSelected();
                return;
            }

            // 检查鼠标是否在 UI 上，如果在 UI 上则不处理地图编辑事件
            if (UiUtils.IsMouseOverAnyUi(GetViewport()))
                return;

            // 鼠标处理
            // - 左键+Ctrl: 选中/多选
            // - 左键无Ctrl: 留给相机拖动视野
            // - 右键: 撤销上一步选择
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        // 根据配置决定是否需要Ctrl键
                        if (!RequireCtrlForSelection || mb.CtrlPressed)
                        {
                            StartSelection(mb);
                        }
                        // 不需要Ctrl时不处理，留给相机拖动
                    }
                    else
                    {
                        // 结束选择（如果有进行中的选择）
                        if (IsSelecting)
                            EndSelection();
                    }
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    if (mb.Pressed)
                    {
                        // 右键撤销上一步选择
                        UndoSelection();
                    }
                }
            }

            if (@event is InputEventMouseMotion mm)
            {
                // 根据配置决定是否需要Ctrl键才能更新选择
                if (IsSelecting && (!RequireCtrlForSelection || mm.CtrlPressed))
                {
                    UpdateSelection(mm);
                }
            }
        }

        public override void _Draw()
        {
            if (!IsEditing || GridManager == null)
                return;

            // 绘制选中的格子（红色边框）
            // 线宽根据相机zoom自适应，确保在屏幕上始终清晰可见
            float selectionLineWidth = 3.0f;
            if (Camera != null)
                selectionLineWidth = Mathf.Max(3.0f / Camera.Zoom.X, 1.0f);

            foreach (var pos in SelectedCells.Keys)
            {
                var worldPos = GridManager.GridToWorld(pos);
                var rect = new Rect2(
                    worldPos - new Vector2(GridManager.GridSize / 2.0f, GridManager.GridSize / 2.0f),
                    new Vector2(GridManager.GridSize, GridManager.GridSize)
                );
                DrawRect(rect, Colors.Red, false, selectionLineWidth);
            }

            // 绘制框选区域预览（半透明红色）
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
                        if (GridManager.IsInBounds(gridPos))
                        {
                            var worldPos = GridManager.GridToWorld(gridPos);
                            var rect = new Rect2(
                                worldPos - new Vector2(GridManager.GridSize / 2.0f, GridManager.GridSize / 2.0f),
                                new Vector2(GridManager.GridSize, GridManager.GridSize)
                            );
                            DrawRect(rect, new Color(1, 0, 0, 0.3f), true);
                        }
                    }
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

            // 兜底：如果 GridManager 之前获取失败，在这里重新尝试
            if (GridManager == null)
            {
                GridManager = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
                if (GridManager == null)
                    GD.PushError("[MapEditor] SetEditMode: GridManager is still null!");
                else
                    GD.Print($"[MapEditor] SetEditMode: late-acquired GridManager={GridManager.Name}");
            }
            GridManager?.SetEditMode(enabled);

            if (Camera is CameraController camCtrl)
                camCtrl.SetEditorMode(enabled);

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
            await ToSignal(GetTree().CreateTimer(3.0f), "timeout");

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
                await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

                var img = viewport.GetTexture().GetImage();
                var path = ProjectSettings.GlobalizePath($"user://auto_test_grid_zoom_{zoom:F2}.png");
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                img.SavePng(path);
                GD.Print($"[MapEditor] Auto-test: screenshot saved to {path}");
            }

            await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
            GD.Print("[MapEditor] Auto-test: quitting...");
            GetTree().Quit();
        }

        private void EnterEditMode()
        {
            GD.Print("[MapEditor] 进入编辑模式 — 冻结游戏状态，重新加载地图数据");

            // 1. 保存游戏状态快照
            _wasTreePaused = GetTree().Paused;
            _wasPlayerVisible = Player?.Visible ?? false;

            var patrolOverlay = GetTree().GetFirstNodeInGroup("monster_patrol_overlay") as MonsterPatrolOverlay;
            _wasMonsterPatrolOverlayVisible = patrolOverlay?.OverlayEnabled ?? false;

            // 2. 暂停游戏树（冻结所有 _Process/_PhysicsProcess）
            GetTree().Paused = true;
            // 地图编辑器自身和相机控制器需要在 Pause 时继续处理输入
            ProcessMode = ProcessModeEnum.Always;
            if (Camera is Node camNode)
                camNode.ProcessMode = ProcessModeEnum.Always;
            // 编辑器面板及其子节点也需要保持响应
            if (_editorPanel != null)
                SetProcessModeRecursive(_editorPanel, ProcessModeEnum.Always);

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

            // 4. 重新加载地图数据（从CSV读取最新配置）
            GridManager?.LoadMap(GridManager.CurrentMapName);

            // 5. 清空编辑历史
            SelectedCells.Clear();
            IsSelecting = false;
            _undoStack.Clear();
            _redoStack.Clear();
            _selectionHistory.Clear();

            QueueRedraw();
            ShowEditorUi();
        }

        private void ExitEditMode()
        {
            GD.Print("[MapEditor] 退出编辑模式 — 恢复游戏状态");

            // 1. 恢复游戏树运行，恢复 ProcessMode
            GetTree().Paused = _wasTreePaused;
            ProcessMode = ProcessModeEnum.Inherit;
            if (Camera is Node camNode)
                camNode.ProcessMode = ProcessModeEnum.Inherit;

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

            // 3. 清除选中的格子
            SelectedCells.Clear();
            IsSelecting = false;
            QueueRedraw();

            HideEditorUi();
        }

        private void SetProcessModeRecursive(Node node, ProcessModeEnum mode)
        {
            node.ProcessMode = mode;
            foreach (var child in node.GetChildren())
                SetProcessModeRecursive(child, mode);
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

        private void CreateEditorPanel()
        {
            var canvasLayer = new CanvasLayer();
            canvasLayer.Layer = 10;
            AddChild(canvasLayer);

            _editorPanel = new Control();
            _editorPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _editorPanel.Size = new Vector2(300, 850);
            _editorPanel.Position = new Vector2(-320, 20);
            canvasLayer.AddChild(_editorPanel);

            var panel = new Panel();
            panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _editorPanel.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.Name = "VBoxContainer";
            vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 10);
            _editorPanel.AddChild(vbox);

            // 版本号诊断信息（放在最顶部，确保可见）
            var diagLabel = new Label();
            diagLabel.Name = "DiagLabel";
            diagLabel.Text = $"[修复版本 {GridFixVersion}]";
            diagLabel.AddThemeColorOverride("font_color", Colors.Yellow);
            diagLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(diagLabel);
            vbox.AddChild(new HSeparator());

            // 标题
            var title = new Label();
            title.Name = "EditorTitle";
            title.Text = "🗺️ 地图编辑器";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 18);
            vbox.AddChild(title);

            // 当前地图名
            var mapNameLabel = new Label();
            mapNameLabel.Name = "MapNameLabel";
            mapNameLabel.Text = GridManager != null ? $"当前地图: {GridManager.CurrentMapName}" : "当前地图: --";
            mapNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            mapNameLabel.AddThemeColorOverride("font_color", Colors.Yellow);
            vbox.AddChild(mapNameLabel);

            // 选中状态显示
            var selectionLabel = new Label();
            selectionLabel.Name = "SelectionLabel";
            selectionLabel.Text = "已选中: 0 个格子";
            selectionLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(selectionLabel);

            // 分隔线
            vbox.AddChild(new HSeparator());

            // 属性设置区域
            var propLabel = new Label();
            propLabel.Text = "选中格子属性:";
            propLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(propLabel);

            // 地形类型（动态加载自 TerrainConfigUtil）
            var terrainLabel = new Label();
            terrainLabel.Text = "地形类型:";
            vbox.AddChild(terrainLabel);

            var terrainOption = new OptionButton();
            terrainOption.Name = "TerrainOption";
            foreach (var kvp in TerrainConfigUtil.Configs)
                terrainOption.AddItem(kvp.Value.Name, kvp.Key);
            terrainOption.ItemSelected += OnTerrainSelected;
            vbox.AddChild(terrainOption);

            // 应用按钮
            var applyBtn = new Button();
            applyBtn.Text = "✓ 应用到选中";
            applyBtn.Pressed += ApplyToSelection;
            vbox.AddChild(applyBtn);

            // 撤销/重做按钮
            var undoRedoHbox = new HBoxContainer();
            vbox.AddChild(undoRedoHbox);

            var undoBtn = new Button();
            undoBtn.Text = "↩ 撤销";
            undoBtn.Pressed += Undo;
            undoRedoHbox.AddChild(undoBtn);

            var redoBtn = new Button();
            redoBtn.Name = "RedoBtn";
            redoBtn.Text = "↪ 重做";
            redoBtn.Pressed += Redo;
            undoRedoHbox.AddChild(redoBtn);

            // 分隔线
            vbox.AddChild(new HSeparator());

            // 快速操作按钮
            var quickLabel = new Label();
            quickLabel.Text = "快速操作:";
            vbox.AddChild(quickLabel);

            var btnHbox = new HBoxContainer();
            vbox.AddChild(btnHbox);

            var selectAllBtn = new Button();
            selectAllBtn.Text = "全选";
            selectAllBtn.Pressed += SelectAll;
            btnHbox.AddChild(selectAllBtn);

            var clearBtn = new Button();
            clearBtn.Text = "清空选择";
            clearBtn.Pressed += ClearSelection;
            btnHbox.AddChild(clearBtn);

            var invertBtn = new Button();
            invertBtn.Text = "反选";
            invertBtn.Pressed += InvertSelection;
            btnHbox.AddChild(invertBtn);

            // 分隔线
            vbox.AddChild(new HSeparator());

            // 显示设置
            var displayLabel = new Label();
            displayLabel.Text = "显示设置:";
            vbox.AddChild(displayLabel);

            // 被删除格子颜色
            var removedColorRow = new HBoxContainer();
            // 分隔线
            vbox.AddChild(new HSeparator());

            // 文件操作
            var fileLabel = new Label();
            fileLabel.Text = "文件操作:";
            vbox.AddChild(fileLabel);

            var saveBtn = new Button();
            saveBtn.Text = "💾 保存地图";
            saveBtn.Pressed += OnSavePressed;
            vbox.AddChild(saveBtn);

            var exportBtn = new Button();
            exportBtn.Text = "📤 导出CSV";
            exportBtn.Pressed += OnExportPressed;
            vbox.AddChild(exportBtn);

            var importBtn = new Button();
            importBtn.Text = "📥 导入CSV";
            importBtn.Pressed += OnImportPressed;
            vbox.AddChild(importBtn);

            // 退出按钮
            var exitBtn = new Button();
            exitBtn.Text = "✕ 退出编辑 (E)";
            exitBtn.Pressed += () => SetEditMode(false);
            vbox.AddChild(exitBtn);

            // 提示
            var hint = new Label();
            hint.Text = "提示: 左键拖动视野, Ctrl+左键选中/多选, 右键撤销选择, Delete删除";
            hint.AddThemeColorOverride("font_color", Colors.Gray);
            hint.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(hint);

            _editorPanel.Visible = false;
        }

        // ============ 选中系统 ============

        private void StartSelection(InputEventMouseButton @event)
        {
            if (GridManager == null)
                return;

            var mousePos = GetGlobalMousePosition();
            var gridPos = GridManager.WorldToGrid(mousePos);

            if (!GridManager.IsInBounds(gridPos))
                return;

            // 保存当前选择状态到历史（用于右键撤销）
            SaveSelectionHistory();

            // 开始框选（包括 Ctrl+拖拽多选）
            IsSelecting = true;
            SelectionStart = gridPos;
            SelectionEnd = gridPos;
            QueueRedraw();
        }

        private void UpdateSelection(InputEventMouseMotion @event)
        {
            if (GridManager == null)
                return;

            var mousePos = GetGlobalMousePosition();
            var gridPos = GridManager.WorldToGrid(mousePos);

            if (GridManager.IsInBounds(gridPos))
            {
                SelectionEnd = gridPos;
                QueueRedraw();
            }
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
                if (!Input.IsKeyPressed(Key.Ctrl))
                    SelectedCells.Clear();
                SelectedCells[SelectionStart] = true;
            }
            else
            {
                // 框选：框内的格子加入选择
                if (!Input.IsKeyPressed(Key.Ctrl))
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
                        if (GridManager.IsInBounds(gridPos))
                            SelectedCells[gridPos] = true;
                    }
                }
            }

            UpdateSelectionLabel();
            QueueRedraw();
        }

        private void SelectAll()
        {
            if (GridManager == null)
                return;

            SelectedCells.Clear();
            for (int y = 0; y < GridManager.MapHeight; y++)
            {
                for (int x = 0; x < GridManager.MapWidth; x++)
                {
                    SelectedCells[new Vector2I(x, y)] = true;
                }
            }

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
            for (int y = 0; y < GridManager.MapHeight; y++)
            {
                for (int x = 0; x < GridManager.MapWidth; x++)
                {
                    var pos = new Vector2I(x, y);
                    if (!SelectedCells.ContainsKey(pos))
                        newSelection[pos] = true;
                }
            }

            SelectedCells = newSelection;
            UpdateSelectionLabel();
            QueueRedraw();
        }

        private void DeleteSelected()
        {
            if (SelectedCells.Count == 0 || GridManager == null)
                return;

            // 在修改前记录撤销命令
            var cmd = CreateUndoCommand(9);
            if (cmd.Changes.Count == 0) return;

            _undoStack.Add(cmd);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();

            foreach (var (pos, oldTerrain) in cmd.Changes)
            {
                var cell = GridManager.GetCell(pos);
                if (cell == null) continue;
                // Delete 键将格子设为地形墙（不可行走的灰色障碍）
                cell.TerrainType = 9;
                cell.TerrainConfig = TerrainConfigUtil.Get(9);
                GD.Print($"[MapEditor.Delete] pos=({pos.X},{pos.Y}) old={oldTerrain} new=9(wall)");
            }

            GridManager.NotifyTerrainChanged();
            GD.Print("[MapEditor] 将 " + cmd.Changes.Count + " 个格子设为地形墙");
        }

        private void UpdateSelectionLabel()
        {
            if (_editorPanel != null)
            {
                var label = _editorPanel.GetNodeOrNull<Label>("SelectionLabel");
                if (label != null)
                    label.Text = $"已选中: {SelectedCells.Count} 个格子";
            }
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

        // ============ UI回调 ============

        private void OnTerrainSelected(long index)
        {
            var terrainOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/TerrainOption");
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

            // 在执行 Undo 前，创建反向命令存入 redo 栈
            var cmd = _undoStack[_undoStack.Count - 1];
            var reverseCmd = new TerrainEditCommand { NewTerrainType = cmd.NewTerrainType };
            foreach (var (pos, _) in cmd.Changes)
            {
                if (!GridManager.IsInBounds(pos)) continue;
                var cell = GridManager.GetCell(pos);
                if (cell == null) continue;
                reverseCmd.Changes.Add((pos, cell.TerrainType));
            }
            _redoStack.Add(reverseCmd);
            if (_redoStack.Count > MaxUndoSteps)
                _redoStack.RemoveAt(0);

            _undoStack.RemoveAt(_undoStack.Count - 1);
            cmd.Undo(GridManager);
            GD.Print("[MapEditor] 撤销操作");
        }

        private void Redo()
        {
            if (_redoStack.Count == 0 || GridManager == null)
            {
                GD.Print("[MapEditor] 没有可重做的操作");
                return;
            }

            // 在执行 Redo 前，创建反向命令存入 undo 栈
            var cmd = _redoStack[_redoStack.Count - 1];
            var reverseCmd = new TerrainEditCommand { NewTerrainType = cmd.NewTerrainType };
            foreach (var (pos, _) in cmd.Changes)
            {
                if (!GridManager.IsInBounds(pos)) continue;
                var cell = GridManager.GetCell(pos);
                if (cell == null) continue;
                reverseCmd.Changes.Add((pos, cell.TerrainType));
            }
            _undoStack.Add(reverseCmd);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);

            _redoStack.RemoveAt(_redoStack.Count - 1);
            cmd.Redo(GridManager);
            GD.Print("[MapEditor] 重做操作");
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
            };
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
            dialog.Filters = new[] { "*.csv" };
            dialog.CurrentFile = GridManager.CurrentMapName + ".csv";
            dialog.FileSelected += DoExport;
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(800, 600));
        }

        private void DoExport(string path)
        {
            if (GridManager == null)
                return;
            var err = GridManager.ExportCsv(path);
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
            dialog.Filters = new[] { "*.csv" };
            dialog.FileSelected += DoImport;
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(800, 600));
        }

        private void DoImport(string path)
        {
            if (GridManager == null)
                return;
            var err = GridManager.ImportCsv(path);
            if (err == Error.Ok)
                GD.Print("[MapEditor] 导入成功: " + path);
            else
                GD.PushError("[MapEditor] 导入失败: " + err);
        }
    }
}
