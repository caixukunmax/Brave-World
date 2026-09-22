using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图编辑器插件主面板（挂在编辑器底部）。左为工具区，右为大画布（SubViewport）。
    /// 画布内承载 GridManager（渲染地形/网格）+ Camera2D（平移/缩放）+ MapEditViewport（输入）+ MapEditDecorationOverlay（装饰绘制）。
    /// 所有数据编辑走共享的 MapEditController。
    /// </summary>
    [Tool]
    public partial class MapEditorBottomPanel : Control
    {
        private MapEditController _controller;
        private GridManager _grid;
        private Camera2D _camera;
        private MapEditViewport _viewport;
        private MapEditDecorationOverlay _overlay;
        private Node2D _decorationLayer;
        private SubViewport _sub;
        private MapEditViewport _subContainer;
        private System.Collections.Generic.Dictionary<int, EntityProfile> _profiles = new();
        private string _decorationSignature = "";

        private OptionButton _mapOption;
        private HSlider _zoomSlider;
        private VBoxContainer _terrainPalette;
        private VBoxContainer _buildingPalette;
        private LineEdit _buildingSearch;
        private Label _status;
        private List<string> _mapNames = new();

        private Button _btnTerrain;
        private Button _btnDecoration;

        public override void _Ready()
        {
            // Dock 本体在两个方向吃满 EditorDock 分到的区域；裁剪越界内容——
            // 左侧工具列内容多、最小高度大，Dock 高度不够时子节点会向下溢出，
            // 盖住 Godot 底部“输出/调试器/音频/动画”标签栏并抢走点击（不裁剪则标签栏无法点击）
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            ClipContents = true;

            // 配置加载（编辑器安全，不依赖 EntityProfileManager 运行时单例）
            TerrainConfigUtil.Load();
            _profiles = ProfileConfigIO.LoadFromFile();
            DecorationConfigUtil.LoadFromProfiles(_profiles);

            BuildUi();
            BuildCanvas();
            RefreshMapList();

            if (_mapNames.Count > 0)
            {
                LoadMap(_mapNames[0]);
            }
            else
            {
                MapEditController.CreateNewMap("new_map", 50, 50);
                RefreshMapList();
                if (_mapNames.Count > 0) LoadMap(_mapNames[0]);
            }

            CenterCamera();
            SetStatus("就绪");
            DumpDiagnostics("初始加载后");
        }

        // 容器布局完成后（首次拿到真实尺寸）把 SubViewport 同步为容器尺寸。
        // 构建时容器只有 2x2，必须等 Resized 再纠正，否则渲染目标塌缩、画布全黑。
        private bool _subSizeSynced;

        private void OnContainerResized()
        {
            if (_sub == null || _subContainer == null) return;
            var s = _subContainer.Size;
            if (s.X < 8 || s.Y < 8) return; // 忽略布局前的塌缩尺寸
            _sub.Size = (Vector2I)s;
            if (!_subSizeSynced)
            {
                _subSizeSynced = true;
                GD.Print($"[MapEditorPlugin] SubViewport 尺寸已同步容器: {_sub.Size}");
            }
        }

        /// <summary>滚轮缩放后同步缩放滑条（不触发 ValueChanged，避免回环）。</summary>
        public void SyncZoomSlider(float z)
        {
            _zoomSlider?.SetValueNoSignal(z);
        }

        /// <summary>输出渲染链路诊断信息到编辑器 Output 面板，用于定位"画布空白"问题。</summary>
        private void DumpDiagnostics(string tag)        {
            var shaderOverlay = _grid?.GetNodeOrNull<GridShaderOverlay>("GridShaderOverlay");
            GD.Print($"[MapEditorPlugin] {tag}: gridInTree={_grid?.IsInsideTree()}, " +
                $"cells={_grid?.GridData?.Count ?? -1}, bounds={_grid?.MapBounds}, " +
                $"shaderOverlay={(shaderOverlay != null ? "存在" : "缺失")}, " +
                $"camPos={_camera?.GlobalPosition}, camEnabled={_camera?.Enabled}, " +
                $"subInTree={_sub?.IsInsideTree()}, subSize={_sub?.Size}");
        }

        // ============ UI 构建 ============

        private void BuildUi()
        {
            var root = new HBoxContainer();
            // 本类是普通 Control 而非容器，子节点 SizeFlags 不生效——根容器必须靠
            // FullRect 锚点充满 Dock，否则保持最小尺寸并向下溢出盖住底部标签栏
            root.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(root);

            // ---- 左侧工具区（外包 ScrollContainer：列内容多、最小高度大，
            // Dock 高度不够时滚动，而不是溢出盖住 Godot 底部标签栏）----
            var leftScroll = new ScrollContainer();
            leftScroll.CustomMinimumSize = new Vector2I(300, 0);
            leftScroll.SizeFlagsHorizontal = SizeFlags.Fill;
            leftScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(leftScroll);

            var left = new VBoxContainer();
            left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftScroll.AddChild(left);

            left.AddChild(MakeLabel("地图"));
            var mapRow = new HBoxContainer();
            _mapOption = new OptionButton();
            _mapOption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _mapOption.ItemSelected += OnMapSelected;
            mapRow.AddChild(_mapOption);
            left.AddChild(mapRow);

            var mapBtnRow = new HBoxContainer();
            mapBtnRow.AddChild(MakeButton("新建", OnNewMap));
            mapBtnRow.AddChild(MakeButton("重命名", OnRenameMap));
            mapBtnRow.AddChild(MakeButton("删除", OnDeleteMap));
            left.AddChild(mapBtnRow);

            left.AddChild(MakeSeparator());
            left.AddChild(MakeLabel("工具"));
            var toolRow = new HBoxContainer();
            _btnTerrain = MakeButton("刷地形", () => SetTool(MapEditController.MapEditTool.PaintTerrain));
            _btnDecoration = MakeButton("放建筑", () => SetTool(MapEditController.MapEditTool.PlaceDecoration));
            toolRow.AddChild(_btnTerrain);
            toolRow.AddChild(_btnDecoration);
            left.AddChild(toolRow);
            SetTool(MapEditController.MapEditTool.PaintTerrain);

            left.AddChild(MakeSeparator());
            left.AddChild(MakeLabel("地形调色板（选中后点击应用）"));
            var terrainScroll = new ScrollContainer();
            terrainScroll.CustomMinimumSize = new Vector2I(0, 160);
            terrainScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            terrainScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _terrainPalette = new VBoxContainer();
            _terrainPalette.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            terrainScroll.AddChild(_terrainPalette);
            left.AddChild(terrainScroll);
            BuildTerrainPalette();

            left.AddChild(MakeSeparator());
            left.AddChild(MakeLabel("建筑图鉴（点击设为笔刷）"));
            _buildingSearch = new LineEdit();
            _buildingSearch.PlaceholderText = "搜索建筑名…";
            _buildingSearch.TextChanged += OnBuildingSearchChanged;
            left.AddChild(_buildingSearch);
            var buildScroll = new ScrollContainer();
            buildScroll.CustomMinimumSize = new Vector2I(0, 200);
            buildScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            buildScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _buildingPalette = new VBoxContainer();
            _buildingPalette.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            buildScroll.AddChild(_buildingPalette);
            left.AddChild(buildScroll);
            BuildBuildingPalette("");

            left.AddChild(MakeSeparator());
            var actRow1 = new HBoxContainer();
            actRow1.AddChild(MakeButton("保存", OnSave));
            actRow1.AddChild(MakeButton("撤销", () => _controller.Undo()));
            actRow1.AddChild(MakeButton("重做", () => _controller.Redo()));
            left.AddChild(actRow1);
            var actRow2 = new HBoxContainer();
            actRow2.AddChild(MakeButton("全选", () => _controller.SelectAll()));
            actRow2.AddChild(MakeButton("清空选择", () => _controller.ClearSelection()));
            actRow2.AddChild(MakeButton("开辟地图", OnExtend));
            left.AddChild(actRow2);

            left.AddChild(MakeSeparator());
            left.AddChild(MakeLabel("缩放"));
            _zoomSlider = new HSlider();
            _zoomSlider.MinValue = 0.2; _zoomSlider.MaxValue = 4; _zoomSlider.Step = 0.1; _zoomSlider.Value = 1;
            _zoomSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _zoomSlider.ValueChanged += (v) => { if (_camera != null) _camera.Zoom = new Vector2((float)v, (float)v); };
            left.AddChild(_zoomSlider);

            _status = MakeLabel("");
            left.AddChild(_status);

            // ---- 右侧画布 ----
            _subContainer = new MapEditViewport();
            _subContainer.MouseFilter = Control.MouseFilterEnum.Stop;
            _subContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _subContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            var gw = ProjectSettings.GetSetting("display/window/size/viewport_width", 1280).AsInt32();
            var gh = ProjectSettings.GetSetting("display/window/size/viewport_height", 720).AsInt32();
            if (gw > 0 && gh > 0) _subContainer.GameAspect = gw / (float)gh;
            root.AddChild(_subContainer);
        }

        private void BuildCanvas()
        {
            _sub = new SubViewport();
            // 不用 Stretch：容器构建时尺寸是 2x2，StretchShrink=1 会把 SubViewport 同步压成 2x2，
            // 渲染目标塌缩导致画布全黑（且引擎禁止此时手动改 Size）。
            // 改为监听容器 Resized，手动让 SubViewport 与容器同尺寸，1:1 显示。
            // 初始渲染目标给小值：SubViewportContainer(Stretch=false) 会把 SubViewport.Size 当作自身
            // 最小尺寸，1920x1080 会把整个底部 Dock 顶到屏幕外，挤掉“输出/调试器”标签栏使其无法点击。
            // 真实尺寸在容器 Resized 后同步；布局下限由 MapEditViewport._GetMinimumSize 兜底。
            _sub.Size = new Vector2I(320, 180);
            // 防御：显式清空 2D 覆盖（size_2d_override_stretch 会把内容非等比拉伸，表现为横竖条纹）
            _sub.Size2DOverride = Vector2I.Zero;
            _sub.Size2DOverrideStretch = false;
            // 编辑器下 WhenVisible 的可见性判定不一定可靠，常驻更新避免渲染目标不刷新
            _sub.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _subContainer.AddChild(_sub);
            _subContainer.Resized += OnContainerResized;

            _grid = new GridManager { GridSize = 48, SkipAutoLoad = true };
            _sub.AddChild(_grid);

            _decorationLayer = new Node2D { Name = "DecorationLayer" };
            _sub.AddChild(_decorationLayer);

            _camera = new Camera2D();
            _sub.AddChild(_camera);

            _overlay = new MapEditDecorationOverlay();
            _overlay.Grid = _grid;
            _sub.AddChild(_overlay);

            // 输入处理直接放在 SubViewportContainer（编辑器 GUI 树中的 Control）上，
            // 其 _GuiInput 必定触发，绕开 SubViewport 内部节点转发输入在编辑器下不可靠的问题。
            _viewport = _subContainer;
            _viewport.Sub = _sub;
            _viewport.Grid = _grid;
            _viewport.Camera = _camera;
            _viewport.Overlay = _overlay;
            _viewport.Panel = this;

            _controller = new MapEditController(_grid);
            _viewport.Controller = _controller;
            _overlay.Controller = _controller;
            _controller.OnVisualChanged += () =>
            {
                _grid.UpdateGridShaderOverlay();
                _grid.NotifyTerrainChanged();
                _overlay.QueueRedraw();
                RefreshDecorations();
            };
        }

        // ============ 调色板 ============

        private void BuildTerrainPalette()
        {
            _terrainPalette.ClearChildren();
            // 清除按钮
            _terrainPalette.AddChild(MakeButton("清除地形 (0)", () => _controller.ApplyDecorationTypeToSelection(0)));
            foreach (var kvp in DecorationConfigUtil.Configs.OrderBy(k => k.Key))
            {
                var cfg = kvp.Value;
                if (cfg.Category != "Terrain") continue;
                _terrainPalette.AddChild(MakeButton($"{cfg.DisplayName} ({cfg.Id})", () => _controller.ApplyDecorationTypeToSelection(cfg.Id)));
            }
        }

        private void BuildBuildingPalette(string filter)
        {
            _buildingPalette.ClearChildren();
            var f = (filter ?? "").Trim().ToLower();
            foreach (var kvp in DecorationConfigUtil.Configs.OrderBy(k => k.Key))
            {
                var cfg = kvp.Value;
                if (cfg.Id == 0) continue;
                if (cfg.Category == "Terrain") continue; // 地形类只在地形调色板
                if (!string.IsNullOrEmpty(f) &&
                    !(cfg.DisplayName ?? "").ToLower().Contains(f) &&
                    !(cfg.Name ?? "").ToLower().Contains(f) &&
                    !cfg.Id.ToString().Contains(f))
                    continue;
                _buildingPalette.AddChild(MakeButton($"{cfg.DisplayName} ({cfg.Id}, {cfg.SizeX}x{cfg.SizeY})", () =>
                {
                    _viewport.CurrentDecorationId = cfg.Id;
                    SetTool(MapEditController.MapEditTool.PlaceDecoration);
                    SetStatus($"笔刷：{cfg.DisplayName}");
                }));
            }
        }

        private void OnBuildingSearchChanged(string text) => BuildBuildingPalette(text);

        // ============ 地图管理 ============

        private void RefreshMapList()
        {
            _mapNames = MapEditController.GetMapList();
            _mapOption.Clear();
            foreach (var name in _mapNames)
                _mapOption.AddItem(name);
        }

        private void OnMapSelected(long index)
        {
            if (index < 0 || index >= _mapNames.Count) return;
            LoadMap(_mapNames[(int)index]);
            CenterCamera();
        }

        private void LoadMap(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _decorationSignature = "";
            if (_controller.LoadMap(name))
                SetStatus($"已加载地图：{name}");
            else
                SetStatus($"加载失败：{name}", Colors.Red);
        }

        private void OnNewMap()
        {
            ShowTextDialog("新建地图", "地图名（英文/数字）", (name) =>
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                if (MapEditController.MapExists(name))
                {
                    SetStatus($"地图已存在：{name}", Colors.Red);
                    return;
                }
                if (MapEditController.CreateNewMap(name, 50, 50))
                {
                    RefreshMapList();
                    // 选中新建的地图
                    int idx = _mapNames.IndexOf(name);
                    if (idx >= 0) _mapOption.Select(idx);
                    LoadMap(name);
                    CenterCamera();
                }
                else
                {
                    SetStatus($"新建失败：{name}", Colors.Red);
                }
            });
        }

        private void OnRenameMap()
        {
            if (_mapNames.Count == 0) return;
            var cur = _mapNames[(int)_mapOption.Selected];
            ShowTextDialog("重命名地图", cur, (newName) =>
            {
                if (string.IsNullOrWhiteSpace(newName) || newName == cur) return;
                if (MapEditController.RenameMap(cur, newName))
                {
                    RefreshMapList();
                    int idx = _mapNames.IndexOf(newName);
                    if (idx >= 0) _mapOption.Select(idx);
                    SetStatus($"已重命名：{cur} -> {newName}");
                }
                else
                {
                    SetStatus("重命名失败", Colors.Red);
                }
            });
        }

        private void OnDeleteMap()
        {
            if (_mapNames.Count == 0) return;
            var cur = _mapNames[(int)_mapOption.Selected];
            var d = new ConfirmationDialog();
            d.Title = "删除地图";
            d.DialogText = $"确定删除地图 '{cur}'？此操作不可恢复。";
            d.Confirmed += () =>
            {
                if (MapEditController.DeleteMap(cur))
                {
                    RefreshMapList();
                    if (_mapNames.Count > 0) { _mapOption.Select(0); LoadMap(_mapNames[0]); CenterCamera(); }
                    SetStatus($"已删除：{cur}");
                }
                d.QueueFree();
            };
            d.Canceled += () => d.QueueFree();
            AddChild(d);
            d.PopupCentered();
        }

        private void OnExtend()
        {
            if (_controller == null) return;
            if (!_controller.HasOutOfBoundsSelection())
            {
                SetStatus("请先用刷地形工具框选到地图边界外的格子", Colors.Yellow);
                return;
            }
            var cells = new List<Vector2I>();
            foreach (var pos in _controller.SelectedCells.Keys)
                if (!_grid.IsInBounds(pos)) cells.Add(pos);
            var sb = _controller.GetSelectionBounds();
            var nb = _grid.MapBounds.Merge(sb);
            var d = new ConfirmationDialog();
            d.Title = "开辟地图";
            d.DialogText = $"将地图边界从 {_grid.MapWidth}x{_grid.MapHeight} 扩展至 {nb.Size.X}x{nb.Size.Y}，新增 {cells.Count} 个格子。确定吗？";
            d.Confirmed += () =>
            {
                if (_controller.ExtendMap(cells))
                {
                    CenterCamera();
                    SetStatus($"地图已扩展至 {_grid.MapWidth}x{_grid.MapHeight}");
                }
                d.QueueFree();
            };
            d.Canceled += () => d.QueueFree();
            AddChild(d);
            d.PopupCentered();
        }

        private void OnSave()
        {
            var err = _controller.SaveMap();
            if (err == Error.Ok) SetStatus($"已保存：{_grid.CurrentMapName}");
            else SetStatus($"保存失败：{err}", Colors.Red);
        }

        private void SetTool(MapEditController.MapEditTool tool)
        {
            if (_viewport != null) _viewport.CurrentTool = tool;
            if (_btnTerrain != null) _btnTerrain.Modulate = tool == MapEditController.MapEditTool.PaintTerrain ? Colors.Cyan : Colors.White;
            if (_btnDecoration != null) _btnDecoration.Modulate = tool == MapEditController.MapEditTool.PlaceDecoration ? Colors.Cyan : Colors.White;
        }

        private void CenterCamera()
        {
            if (_camera == null || _grid == null) return;
            var b = _grid.MapBounds;
            var center = _grid.GridToWorld(new Vector2I(b.Position.X + b.Size.X / 2, b.Position.Y + b.Size.Y / 2));
            _camera.GlobalPosition = center;
        }

        /// <summary>
        /// 用真实 MapDecoration 节点渲染所有装饰，与游戏运行时 1:1 一致。
        /// 通过编辑器安全的 SetupFromProfile（静态 ApplyProfileToEntity，不依赖运行时单例）套用外观，
        /// 因此建筑颜色 / 边框 / 标签 / Sprite 贴图与游戏内完全相同。
        /// </summary>
        private void RefreshDecorations()
        {
            if (_decorationLayer == null || _grid == null || _grid.GridData == null) return;

            // 仅在装饰数据变化时重建，避免刷地形等无关操作重复生成大量节点（野狼谷等地图装饰可达数百）
            var sig = new StringBuilder();
            foreach (var cell in _grid.GridData.Values)
            {
                if (cell.DecorationType != 0)
                    sig.Append(cell.Pos.X).Append(',').Append(cell.Pos.Y).Append(':').Append(cell.DecorationType).Append(';');
            }
            string signature = sig.ToString();
            if (signature == _decorationSignature) return;
            _decorationSignature = signature;

            foreach (var c in _decorationLayer.GetChildren().ToArray())
                c.QueueFree();

            var occupied = new HashSet<Vector2I>();
            int gs = _grid.GridSize;
            foreach (var cell in _grid.GridData.Values)
            {
                if (cell.DecorationType == 0) continue;
                if (occupied.Contains(cell.Pos)) continue;
                if (!_profiles.TryGetValue(cell.DecorationType, out var profile) || profile == null)
                    continue;

                var dec = new MapDecoration();
                dec.IsEditable = false;
                dec.SetProcessInput(false);
                dec.SetProcess(false);
                dec.SetupFromProfile(profile, registerInDecorationGroup: false, gridX: cell.Pos.X, gridY: cell.Pos.Y, gridSize: gs);
                _decorationLayer.AddChild(dec);

                int sx = dec.SizeX, sy = dec.SizeY;
                for (int dx = 0; dx < sx; dx++)
                    for (int dy = 0; dy < sy; dy++)
                        occupied.Add(new Vector2I(cell.Pos.X + dx, cell.Pos.Y + dy));
            }
        }

        // ============ 小工具 ============

        private void SetStatus(string text, Color? color = null)
        {
            if (_status == null) return;
            _status.Text = text;
            _status.Modulate = color ?? Colors.White;
        }

        private static Label MakeLabel(string text)
        {
            var l = new Label();
            l.Text = text;
            return l;
        }

        private static Button MakeButton(string text, System.Action onPressed)
        {
            var b = new Button();
            b.Text = text;
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            b.Pressed += onPressed;
            return b;
        }

        private static HSeparator MakeSeparator()
        {
            return new HSeparator();
        }

        private void ShowTextDialog(string title, string placeholder, System.Action<string> onConfirm)
        {
            var w = new Window();
            w.Title = title;
            w.Size = new Vector2I(380, 150);
            var vbox = new VBoxContainer();
            w.AddChild(vbox);
            var le = new LineEdit();
            le.PlaceholderText = placeholder;
            le.Text = placeholder;
            vbox.AddChild(le);
            var hbox = new HBoxContainer();
            vbox.AddChild(hbox);
            var ok = new Button(); ok.Text = "确定"; ok.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var cancel = new Button(); cancel.Text = "取消"; cancel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hbox.AddChild(ok); hbox.AddChild(cancel);
            ok.Pressed += () => { onConfirm(le.Text.Trim()); w.QueueFree(); };
            cancel.Pressed += () => w.QueueFree();
            AddChild(w);
            w.PopupCentered();
        }
    }
}
