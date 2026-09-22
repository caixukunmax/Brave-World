using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    public partial class MapEditor : Node2D
    {
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
            hint.Text = "提示: 刷地形工具下按住 Ctrl 拖拽框选，松开确定选区后点击“应用到选中”；未按 Ctrl 时拖拽移动视野。放建筑工具下从列表按住房舍拖到地图放置，拖动已放置建筑换位，Delete/删除按钮移除悬停建筑；右键地图外格子可开辟地图";
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

            // 切换工具时清除选区，避免残留选区干扰新工具操作
            SelectedCells.Clear();
            IsSelecting = false;
            QueueRedraw();

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
            // 地形类建筑选择
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
            // 只显示 Category == "Terrain" 的建筑（树、草地、水、岩石等）
            // 按 ID 升序固定添加，避免 Dictionary 遍历顺序导致下拉栏显示与实际 ID 错位
            var terrainConfigs = DecorationConfigUtil.Configs
                .Where(kvp => kvp.Key != 0)
                .Where(kvp => IsValidTerrainDecorationId(kvp.Key))
                .Where(kvp => kvp.Value.Category == "Terrain")
                .OrderBy(kvp => kvp.Key)
                .ToList();
            foreach (var kvp in terrainConfigs)
            {
                terrainOption.AddItem(kvp.Value.DisplayName, kvp.Key);
                LogDiagnostic($"[MapEditor.CreateTerrainToolContent] 添加地形选项: id={kvp.Key}, name={kvp.Value.DisplayName}");
            }
            // 增加"清除"选项，用于批量清除选区 decoration
            terrainOption.AddItem("(清除)", 0);

            // 在控件加入场景树后再同步选中状态，避免初始化期间信号/显示异常
            // 先连接信号，随后 SyncTerrainDropdownSelection 会临时断开并恢复
            terrainOption.ItemSelected += OnTerrainDecorationSelected;
            terrainRow.AddChild(terrainOption);

            SyncTerrainDropdownSelection(terrainOption);

            // 应用按钮
            var applyBtn = new Button();
            applyBtn.Name = "TerrainApplyBtn";
            applyBtn.Text = "✓ 应用到选中";
            applyBtn.CustomMinimumSize = new Vector2(0, 26);
            applyBtn.Pressed += ApplyToSelection;
            parent.AddChild(applyBtn);
        }

        /// <summary>
        /// 同步地形下拉栏的选中项与 PaintTerrainDecoration。
        /// 如果当前 PaintTerrainDecoration 在列表中，则选中对应项；
        /// 否则选中第一个有效地形项，并将 PaintTerrainDecoration 更新为该 ID。
        /// 0、1-3、10002 等无效值会被视为需要回退到第一个有效地形项。
        /// </summary>
        private void SyncTerrainDropdownSelection(OptionButton terrainOption)
        {
            // 临时断开信号，防止 Select 触发 ItemSelected 改写 PaintTerrainDecoration
            terrainOption.ItemSelected -= OnTerrainDecorationSelected;
            try
            {
                int selectedIndex = -1;
                for (int i = 0; i < terrainOption.ItemCount; i++)
                {
                    if (terrainOption.GetItemId(i) == PaintTerrainDecoration)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                if (selectedIndex >= 0 && PaintTerrainDecoration != 0 && IsValidTerrainDecorationId(PaintTerrainDecoration))
                {
                    terrainOption.Select(selectedIndex);
                    LogDiagnostic($"[MapEditor.CreateTerrainToolContent] 同步选中: index={selectedIndex}, id={PaintTerrainDecoration}");
                }
                else if (terrainOption.ItemCount > 1)
                {
                    // 当前 PaintTerrainDecoration 不在列表中或为清除/非法值，默认选中第一个有效地形项（非清除）
                    terrainOption.Select(0);
                    PaintTerrainDecoration = terrainOption.GetItemId(0);
                    LogDiagnostic($"[MapEditor.CreateTerrainToolContent] 默认选中第一项: index=0, id={PaintTerrainDecoration}");
                }
            }
            finally
            {
                terrainOption.ItemSelected += OnTerrainDecorationSelected;
            }
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
            var decorationConfigs = new List<DecorationConfig>();
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
    }
}
