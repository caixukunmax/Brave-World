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

        // 选中系统
        public Dictionary<Vector2I, bool> SelectedCells { get; private set; } = new Dictionary<Vector2I, bool>();  // Vector2i -> bool
        public bool IsSelecting { get; private set; } = false;       // 是否正在拖拽选择
        public Vector2I SelectionStart { get; private set; }        // 框选起始格子
        public Vector2I SelectionEnd { get; private set; }          // 框选结束格子

        // 当前设置的属性（应用到选中格子）
        public bool PaintExists { get; set; } = true;
        public bool PaintWalkable { get; set; } = true;
        public bool PaintVisible { get; set; } = true;
        public int PaintTerrain { get; set; } = 0;

        // UI引用
        private Control _editorPanel;

        // 按键配置（可由调试面板设置）
        public bool RequireCtrlForSelection { get; set; } = true;  // 是否需要Ctrl键才能选中

        // 撤销历史
        private System.Collections.Generic.List<List<List<GridCell>>> _undoStack = new();
        public const int MaxUndoSteps = 20;

        // 选择历史（用于右键撤销选择）
        private List<Dictionary<Vector2I, bool>> _selectionHistory = new List<Dictionary<Vector2I, bool>>();  // 每次选择操作前保存选中状态
        public const int MaxSelectionHistory = 10;

        public override async void _Ready()
        {
            // 延迟获取节点引用
            await ToSignal(GetTree(), "process_frame");
            GridManager = GetTree().GetFirstNodeInGroup("grid_manager") as GridManager;
            Camera = GetTree().GetFirstNodeInGroup("camera") as Camera2D;
            Player = GetTree().GetFirstNodeInGroup("player") as Node2D;

            GD.Print("[MapEditor] 初始化完成");
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
                        if (!RequireCtrlForSelection || Input.IsKeyPressed(Key.Ctrl))
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
                if (IsSelecting && (!RequireCtrlForSelection || Input.IsKeyPressed(Key.Ctrl)))
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

            GridManager?.SetEditMode(enabled);

            if (Camera is CameraController camCtrl)
                camCtrl.SetEditorMode(enabled);

            if (Player != null)
                Player.Visible = !enabled;

            // 控制调试面板的可见性（地图编辑器独立于调试面板）
            var debugPanel = GetTree().GetFirstNodeInGroup("debug_panel");
            if (debugPanel != null)
            {
                if (enabled)
                    debugPanel.Call("hide_panel");
                else
                    debugPanel.Call("show_panel");
            }

            if (!enabled)
            {
                // 退出编辑模式时清除选中的格子
                SelectedCells.Clear();
                IsSelecting = false;
                QueueRedraw();
            }

            if (enabled)
            {
                GD.Print("[MapEditor] 进入编辑模式");
                ShowEditorUi();
            }
            else
            {
                GD.Print("[MapEditor] 退出编辑模式");
                HideEditorUi();
                SelectedCells.Clear();
            }
        }

        private void ShowEditorUi()
        {
            if (_editorPanel == null)
                CreateEditorPanel();
            if (_editorPanel != null)
            {
                _editorPanel.Visible = true;
                _editorPanel.MouseFilter = Control.MouseFilterEnum.Stop;

                // 初始化颜色按钮
                var colorBtn = _editorPanel.GetNodeOrNull<Button>("RemovedColorRow/RemovedColorBtn");
                if (colorBtn != null && GridManager != null)
                    colorBtn.AddThemeColorOverride("font_color", GridManager.RemovedCellColor);
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
            _editorPanel.Size = new Vector2(300, 500);
            _editorPanel.Position = new Vector2(-320, 20);
            canvasLayer.AddChild(_editorPanel);

            var panel = new Panel();
            panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _editorPanel.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 10);
            _editorPanel.AddChild(vbox);

            // 标题
            var title = new Label();
            title.Text = "🗺️ 地图编辑器";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 18);
            vbox.AddChild(title);

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

            // 是否存在
            var existsCheck = new CheckButton();
            existsCheck.Name = "ExistsCheck";
            existsCheck.Text = "存在格子";
            existsCheck.ButtonPressed = true;
            existsCheck.Toggled += OnExistsToggled;
            vbox.AddChild(existsCheck);

            // 可行走
            var walkableCheck = new CheckButton();
            walkableCheck.Name = "WalkableCheck";
            walkableCheck.Text = "可行走";
            walkableCheck.ButtonPressed = true;
            walkableCheck.Toggled += OnWalkableToggled;
            vbox.AddChild(walkableCheck);

            // 可见
            var visibleCheck = new CheckButton();
            visibleCheck.Name = "VisibleCheck";
            visibleCheck.Text = "可见";
            visibleCheck.ButtonPressed = true;
            visibleCheck.Toggled += OnVisibleToggled;
            vbox.AddChild(visibleCheck);

            // 地形类型
            var terrainLabel = new Label();
            terrainLabel.Text = "地形类型:";
            vbox.AddChild(terrainLabel);

            var terrainOption = new OptionButton();
            terrainOption.Name = "TerrainOption";
            var terrains = new[] { "普通", "水域", "草地", "沙地", "岩石", "雪地", "沼泽" };
            for (int i = 0; i < terrains.Length; i++)
                terrainOption.AddItem(terrains[i], i);
            terrainOption.ItemSelected += OnTerrainSelected;
            vbox.AddChild(terrainOption);

            // 应用按钮
            var applyBtn = new Button();
            applyBtn.Text = "✓ 应用到选中";
            applyBtn.Pressed += ApplyToSelection;
            vbox.AddChild(applyBtn);

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
            removedColorRow.Name = "RemovedColorRow";
            var removedColorLabel = new Label();
            removedColorLabel.Text = "已删除格子颜色:";
            removedColorLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var removedColorBtn = new Button();
            removedColorBtn.Name = "RemovedColorBtn";
            removedColorBtn.Text = "■";
            removedColorBtn.Pressed += OnRemovedColorPressed;
            removedColorRow.AddChild(removedColorLabel);
            removedColorRow.AddChild(removedColorBtn);
            vbox.AddChild(removedColorRow);

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

            SaveUndoState();

            foreach (var pos in SelectedCells.Keys)
            {
                if (GridManager.IsInBounds(pos))
                {
                    var cell = GridManager.GetCell(pos);
                    if (cell != null)
                    {
                        cell.Exists = false;
                        cell.Walkable = false;  // 被删除的格子不可行走
                    }
                }
            }

            GridManager.QueueRedraw();
            GD.Print("[MapEditor] 删除了 " + SelectedCells.Count + " 个格子");
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

            SaveUndoState();

            foreach (var pos in SelectedCells.Keys)
            {
                if (GridManager.IsInBounds(pos))
                {
                    var cell = GridManager.GetCell(pos);
                    if (cell != null)
                    {
                        cell.Exists = PaintExists;
                        cell.Walkable = PaintWalkable;
                        cell.Visible = PaintVisible;
                        cell.TerrainType = PaintTerrain;
                        cell.RefreshTerrainConfig();
                    }
                }
            }

            GridManager.QueueRedraw();
            GD.Print("[MapEditor] 已应用属性到 " + SelectedCells.Count + " 个格子");
        }

        // ============ UI回调 ============

        private void OnExistsToggled(bool enabled)
        {
            PaintExists = enabled;
        }

        private void OnWalkableToggled(bool enabled)
        {
            PaintWalkable = enabled;
        }

        private void OnVisibleToggled(bool enabled)
        {
            PaintVisible = enabled;
        }

        private void OnTerrainSelected(long index)
        {
            PaintTerrain = (int)index;
        }

        private void OnRemovedColorPressed()
        {
            // 切换被删除格子的颜色
            if (GridManager == null)
                return;

            // 颜色预设：灰色 -> 深灰 -> 红色 -> 蓝色 -> 绿色 -> 灰色
            var colorPresets = new[]
            {
                new Color(0.3f, 0.3f, 0.3f, 0.5f),  // 灰色（默认）
                new Color(0.2f, 0.2f, 0.2f, 0.6f),  // 深灰
                new Color(0.5f, 0.2f, 0.2f, 0.5f),  // 暗红
                new Color(0.2f, 0.2f, 0.5f, 0.5f),  // 暗蓝
                new Color(0.2f, 0.5f, 0.2f, 0.5f),  // 暗绿
            };

            // 找到当前颜色索引，切换到下一个
            var currentColor = GridManager.RemovedCellColor;
            int currentIndex = 0;
            for (int i = 0; i < colorPresets.Length; i++)
            {
                if (currentColor.IsEqualApprox(colorPresets[i]))
                {
                    currentIndex = i;
                    break;
                }
            }

            var nextIndex = (currentIndex + 1) % colorPresets.Length;
            GridManager.RemovedCellColor = colorPresets[nextIndex];

            // 更新按钮颜色
            var btn = _editorPanel?.GetNodeOrNull<Button>("RemovedColorRow/RemovedColorBtn");
            if (btn != null)
                btn.AddThemeColorOverride("font_color", GridManager.RemovedCellColor);

            GridManager.QueueRedraw();
            GD.Print("[MapEditor] 已删除格子颜色改为: " + GridManager.RemovedCellColor);
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

        private void SaveUndoState()
        {
            if (GridManager?.GridData == null || GridManager.GridData.Count == 0)
                return;

            var state = new List<List<GridCell>>();
            foreach (var row in GridManager.GridData)
            {
                var stateRow = new List<GridCell>();
                foreach (var cell in row)
                {
                    var copy = new GridCell(cell.Pos.X, cell.Pos.Y);
                    cell.CopyTo(copy);
                    stateRow.Add(copy);
                }
                state.Add(stateRow);
            }

            _undoStack.Add(state);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);
        }

        private void Undo()
        {
            if (_undoStack.Count == 0)
            {
                GD.Print("[MapEditor] 没有可撤销的操作");
                return;
            }

            var state = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            GridManager.GridData = state;
            GridManager.QueueRedraw();
            GD.Print("[MapEditor] 撤销操作");
        }

        // ============ 文件操作 ============

        private void OnSavePressed()
        {
            if (GridManager == null)
                return;
            var err = GridManager.SaveCurrentMap();
            if (err == Error.Ok)
                GD.Print("[MapEditor] 地图保存成功");
            else
                GD.PushError("[MapEditor] 地图保存失败: " + err);
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
