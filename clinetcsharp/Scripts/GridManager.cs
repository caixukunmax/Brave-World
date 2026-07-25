using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 网格管理器。
    /// [Tool]：地图编辑器插件（addons/map_editor_editor）会在编辑器里手工 new 实例（SkipAutoLoad=true），
    /// 需要 _Ready/_Process/_Draw 在编辑器下执行才能渲染地形/网格；
    /// 游戏场景（main.tscn）中的实例在编辑器下由 _Ready 开头的 Engine.IsEditorHint() 守卫直接跳过，维持原行为。
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridManager : Node2D
    {
        public enum GridLineWidthMode
        {
            FixedWorld = 0,
            FixedScreen = 1,
            AdaptiveCalibration = 2,
        }

        public enum GridSizeMode
        {
            Manual = 0,
            ResponsiveVisibleCount = 1,
        }

        [Export] public int GridSize { get; set; } = 111;
        [Export] public Color LineColor { get; set; } = new Color(0.7f, 0.7f, 0.7f);
        [Export] public float LineWidth { get; set; } = 1.0f;
        [Export] public float DashLength { get; set; } = 8.0f;
        [Export] public float GapLength { get; set; } = 4.0f;

        /// <summary>网格线保证稳定渲染的 zoom 安全区间。在此区间内无论 zoom 如何变化，所有线都能完整渲染。</summary>
        public const float GridRenderMinZoom = 0.1f;
        public const float GridRenderMaxZoom = 5.0f;

        // 线宽自适应设置
        [Export] public bool AutoLineWidth { get; set; } = true;  // 是否启用自动线宽
        [Export] public float LineWidthScale { get; set; } = 1.0f;  // 线宽比例系数（默认 1px）
        [Export] public float MinScreenLineWidth { get; set; } = 1.0f;  // 最小屏幕线宽 1px，低于此值线条不稳定
        [Export] public float MaxScreenLineWidth { get; set; } = 2.0f;  // 最大屏幕线宽 2px，保持"尽可能细"
        [Export] public float GridAntiAliasSoftness { get; set; } = GridOverlayAntiAliasSoftnessPolicy.Default;

        // 线宽自适应校准（新开关模式）
        public bool AdaptiveCalibrationEnabled { get; set; } = false;  // 自适应校准开关
        private float _previewLineWidth = -1.0f;  // 预览线宽（-1表示无预览）

        // 线宽自适应校准参考点（zoom 范围与相机控制器的 min_zoom/max_zoom 保持一致）
        public float RefZoomA { get; set; } = 0.4f;  // 最小 zoom（视野最远）
        public float RefWidthA { get; set; } = 3.0f;  // 对应线宽（视野远时线条较粗，保证可见）
        public float RefZoomB { get; set; } = 1.0f;  // 标准 zoom
        public float RefWidthB { get; set; } = 1.5f;  // 对应线宽（标准视野正常显示）

        /// <summary>地图数据：逻辑坐标 -> 格子。只有存在的格子才会被加入。</summary>
        public System.Collections.Generic.Dictionary<Vector2I, GridCell> GridData { get; set; } = new();
        public string CurrentMapName { get; set; } = "落叶乡";

        /// <summary>当前地图边界（所有存在格子的包围盒）。</summary>
        public Rect2I MapBounds { get; private set; } = new Rect2I(0, 0, 50, 50);

        /// <summary>从 map.json 加载时记录的出生点，保存时作为找不到出生点建筑的回退。</summary>
        private Vector2I _loadedSpawn = new Vector2I(25, 25);

        /// <summary>地图宽度（兼容旧代码，实际为 MapBounds 宽度）</summary>
        public int MapWidth => MapBounds.Size.X;

        /// <summary>地图高度（兼容旧代码，实际为 MapBounds 高度）</summary>
        public int MapHeight => MapBounds.Size.Y;

        /// <summary>地图原点（兼容旧代码，实际为 MapBounds 位置）</summary>
        public Vector2I GridOrigin => MapBounds.Position;

        // 响应式布局设置
        [Export] public bool SkipAutoLoad { get; set; } = false;  // 是否跳过 _Ready 自动加载地图数据（用于 PreviewMap 等手动初始化场景）
        [Export] public bool ResponsiveMode { get; set; } = false;  // 是否启用响应式格子大小
        [Export] public float VisibleGridsX { get; set; } = 5.0f;     // 屏幕横向显示的格子数（支持小数，如5.5）
        [Export] public int MinGridSize { get; set; } = 32;        // 最小格子大小（防止太小）
        [Export] public int MaxGridSize { get; set; } = 256;       // 最大格子大小（防止太大）

        // 编辑模式
        public bool IsEditMode { get; set; } = false;
        public bool ShowGridCoords { get; set; } = false;  // 显示格子逻辑坐标
        public bool ShowTerrainLabels { get; set; } = false;  // 显示地形名称标签
        public bool ShowCellUids { get; set; } = false;  // 显示格子唯一标识符 UID

        // 地图范围外灰色显示设置
        private bool _showOutsideMapGray = false;
        [Export]
        public bool ShowOutsideMapGray
        {
            get => _showOutsideMapGray;
            set
            {
                _showOutsideMapGray = value;
                RefreshOutsideMapVisibility();
            }
        }

        private Color _outsideMapColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
        [Export]
        public Color OutsideMapColor
        {
            get => _outsideMapColor;
            set
            {
                _outsideMapColor = value;
                RefreshOutsideMapVisibility();
            }
        }

        // 自适应校准用的 zoom 跟踪
        private float _lastCameraZoom = 0.0f;
        // 线宽参数变化跟踪（调整参考点、切换编辑模式等需要刷新网格线）
        private bool _lastAdaptiveEnabled = false;
        private bool _lastAutoLineWidth = false;
        private bool _lastIsEditMode = false;
        private float _lastLineWidth = float.NaN;
        private float _lastLineWidthScale = float.NaN;
        private float _lastGridAntiAliasSoftness = float.NaN;
        private float _lastRefZoomA = float.NaN;
        private float _lastRefWidthA = float.NaN;
        private float _lastRefZoomB = float.NaN;
        private float _lastRefWidthB = float.NaN;

        private GridShaderOverlay? _gridShaderOverlay;
        private ColorRect? _background;
        private ImageTexture? _terrainMaskTexture;
        private ImageTexture? _waterMaskTexture;

        public override void _Ready()
        {
            // 编辑器环境下（如用编辑器打开 main.tscn）不做任何初始化：
            // 避免触发 LoadMapData 读写地图文件、连 SizeChanged 信号、修改视口参数等运行时副作用。
            // 地图编辑器插件以 SkipAutoLoad=true 手工创建实例，需要完整初始化渲染链路，不受此守卫影响。
            if (Engine.IsEditorHint() && !SkipAutoLoad)
            {
                SetProcess(false);
#if DEBUG
                GD.Print($"[GridManager] 编辑器守卫：跳过初始化（SkipAutoLoad={SkipAutoLoad}，游戏场景实例正常行为）");
#endif
                return;
            }
#if DEBUG
            if (Engine.IsEditorHint())
                GD.Print("[GridManager] 编辑器插件实例：执行完整初始化（渲染链路启动）");
#endif

            // 初始化地形标签字体
            _terrainLabelFont = ThemeDB.Singleton?.FallbackFont;
            if (_terrainLabelFont == null)
            {
                var sysFont = new SystemFont();
                sysFont.FontNames = new string[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "WenQuanYi Zen Hei" };
                _terrainLabelFont = sysFont;
            }

            if (!SkipAutoLoad)
                LoadMapData();

            // 同步 Background 尺寸
            SyncBackgroundSize();

            // 视口级 DrawRect 网格在开启 2D 像素对齐时更稳定。
            var viewport = GetViewport();
            if (viewport != null)
            {
                viewport.Snap2DTransformsToPixel = true;
                viewport.Snap2DVerticesToPixel = true;
            }

            // 如果启用响应式模式，初始化格子大小
            if (ResponsiveMode)
            {
                UpdateResponsiveGridSize();
                // 监听窗口大小变化
                if (!_isViewportResizedConnected)
                {
                    GetTree().Root.SizeChanged += OnViewportResized;
                    _isViewportResizedConnected = true;
                }
            }

            // 初始化 zoom 跟踪
            _lastCameraZoom = GetCameraZoom();

            // 初始化线宽参数快照，避免 NaN 比较导致首次变化被吞
            _lastAdaptiveEnabled = AdaptiveCalibrationEnabled;
            _lastAutoLineWidth = AutoLineWidth;
            _lastIsEditMode = IsEditMode;
            _lastLineWidth = LineWidth;
            _lastLineWidthScale = LineWidthScale;
            _lastGridAntiAliasSoftness = GridAntiAliasSoftness;
            _lastRefZoomA = RefZoomA;
            _lastRefWidthA = RefWidthA;
            _lastRefZoomB = RefZoomB;
            _lastRefWidthB = RefWidthB;

            EnsureGridShaderOverlay();
            UpdateGridShaderOverlay();
            UpdateTerrainMask();

            SetupHoverCoordLabel();

            QueueRedraw();
        }

        // ========== 鼠标悬停格子坐标（运行时调试，制作演出脚本拾取坐标用） ==========
        /// <summary>悬停坐标显示开关（静态，供调试面板/GM 控制）</summary>
        public static bool ShowHoverGridCoord { get; set; } = true;
        private Label _hoverCoordLabel;

        private void SetupHoverCoordLabel()
        {
            _hoverCoordLabel = new Label
            {
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 100,
            };
            _hoverCoordLabel.AddThemeFontSizeOverride("font_size", 14);
            _hoverCoordLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.6f));
            _hoverCoordLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
            _hoverCoordLabel.AddThemeConstantOverride("outline_size", 4);
            AddChild(_hoverCoordLabel);
        }

        /// <summary>每帧刷新鼠标悬停格子的坐标标签</summary>
        private void UpdateHoverCoordLabel()
        {
            if (_hoverCoordLabel == null)
                return;

            if (!ShowHoverGridCoord || IsEditMode)
            {
                _hoverCoordLabel.Visible = false;
                return;
            }

            var gridPos = WorldToGrid(GetGlobalMousePosition());
            _hoverCoordLabel.Text = $"({gridPos.X}, {gridPos.Y})";
            // 标签是世界子节点，跟随缩放；抬高到光标上方避免遮挡
            _hoverCoordLabel.Position = GetLocalMousePosition() + new Vector2(12, -22) / Mathf.Max(0.1f, GetCameraZoom());
            _hoverCoordLabel.Visible = true;
        }

        public override void _ExitTree()
        {
            // 防止响应式模式事件泄漏到已释放实例
            if (_isViewportResizedConnected)
            {
                GetTree().Root.SizeChanged -= OnViewportResized;
                _isViewportResizedConnected = false;
            }
            base._ExitTree();
        }

        public override void _Process(double _delta)
        {
            UpdateHoverCoordLabel();

            var currentZoom = GetCameraZoom();

            bool zoomChanged = Mathf.Abs(currentZoom - _lastCameraZoom) > 0.001f;

            // 线宽参数变化检测（调整参考点、切换编辑模式、改线宽设置等）
            bool paramsChanged =
                _lastAdaptiveEnabled != AdaptiveCalibrationEnabled ||
                _lastAutoLineWidth != AutoLineWidth ||
                _lastIsEditMode != IsEditMode ||
                Mathf.Abs(_lastLineWidth - LineWidth) > 0.001f ||
                Mathf.Abs(_lastLineWidthScale - LineWidthScale) > 0.001f ||
                Mathf.Abs(_lastGridAntiAliasSoftness - GridAntiAliasSoftness) > 0.001f ||
                Mathf.Abs(_lastRefZoomA - RefZoomA) > 0.001f ||
                Mathf.Abs(_lastRefWidthA - RefWidthA) > 0.001f ||
                Mathf.Abs(_lastRefZoomB - RefZoomB) > 0.001f ||
                Mathf.Abs(_lastRefWidthB - RefWidthB) > 0.001f;

            if (zoomChanged || paramsChanged)
            {
                _lastCameraZoom = currentZoom;

                if (paramsChanged)
                {
                    _lastAdaptiveEnabled = AdaptiveCalibrationEnabled;
                    _lastAutoLineWidth = AutoLineWidth;
                    _lastIsEditMode = IsEditMode;
                    _lastLineWidth = LineWidth;
                    _lastLineWidthScale = LineWidthScale;
                    _lastGridAntiAliasSoftness = GridAntiAliasSoftness;
                    _lastRefZoomA = RefZoomA;
                    _lastRefWidthA = RefWidthA;
                    _lastRefZoomB = RefZoomB;
                    _lastRefWidthB = RefWidthB;
                }

                UpdateGridShaderOverlay();

                QueueRedraw();
            }
        }

        private void EnsureGridShaderOverlay()
        {
            if (_gridShaderOverlay != null && IsInstanceValid(_gridShaderOverlay))
                return;

            _gridShaderOverlay = GetNodeOrNull<GridShaderOverlay>("GridShaderOverlay");
            if (_gridShaderOverlay != null)
                return;

            _gridShaderOverlay = new GridShaderOverlay();
            _gridShaderOverlay.Name = "GridShaderOverlay";
            AddChild(_gridShaderOverlay);
        }

        public void UpdateGridShaderOverlay()
        {
            EnsureGridShaderOverlay();
            if (_gridShaderOverlay == null)
                return;

            var cameraZoom = Mathf.Clamp(GetCameraZoom(), GridRenderMinZoom, GridRenderMaxZoom);
            var (lineWidthWorld, lineColor) = ComputeGridLineRenderStyle(cameraZoom);
            float screenLineWidth = lineWidthWorld * cameraZoom;

            _gridShaderOverlay.UpdateOverlay(
                GridSize,
                MapBounds,
                screenLineWidth,
                lineColor,
                GridAntiAliasSoftness);

            _gridShaderOverlay.UpdateOutsideMapColor(ShowOutsideMapGray ? OutsideMapColor : Colors.Transparent);
        }

        private void LoadMapData()
        {
            // 尝试从 map.json 加载
            var loadedData = MapDataManager.LoadMapFromJson(
                CurrentMapName, out var loadedBounds, out var loadedSpawn, out var loadedDisplayName);

            if (loadedData.Count > 0)
            {
                GridData = loadedData;
                MapBounds = loadedBounds;
                _loadedSpawn = loadedSpawn;
                RecalculateMapBounds();
            #if DEBUG
            GD.Print($"[GridManager] 加载地图: {CurrentMapName} 格子数={GridData.Count}, bounds={MapBounds} spawn={_loadedSpawn}");
            #endif
            }
            else if (FileAccess.FileExists(MapDataManager.MapsFolder + CurrentMapName + "/" + MapDataManager.JsonFilename))
            {
                // JSON 存在但 cells 为空：保持声明的 bounds
                MapBounds = loadedBounds;
                #if DEBUG
                GD.Print($"[GridManager] 加载地图: {CurrentMapName} 格子数=0, bounds={MapBounds}");
                #endif
            }
            else
            {
                // 创建默认地图
                #if DEBUG
            GD.Print("[GridManager] 创建默认地图数据");
            #endif
                CreateDefaultGridData();
                // 保存默认地图
                if (!MapDataManager.MapExists(CurrentMapName))
                    MapDataManager.CreateNewMap(CurrentMapName, MapBounds.Size.X, MapBounds.Size.Y);
                else
                    MapDataManager.SaveMapToJson(CurrentMapName, GridData, CurrentMapName, MapBounds, new Vector2I(25, 25));
            }

            UpdateTerrainMask();
            SyncDecorations();
        }

        /// <summary>
        /// 通知 MapDecorationManager 根据当前 GridData 刷新装饰摆件。
        /// 编辑模式下优先使用 edit_decoration_manager 组中的管理器。
        /// </summary>
        public void SyncDecorations()
        {
            var decMgr = GetTree()?.GetFirstNodeInGroup("edit_decoration_manager") as MapDecorationManager
                ?? GetTree()?.GetFirstNodeInGroup("map_decoration_manager") as MapDecorationManager;
            if (decMgr == null) return;
            decMgr.GridSize = GridSize;
            decMgr.SpawnDecorations(GridData);
            RebuildBlockedByDecoration();
        }

        private void CreateDefaultGridData()
        {
            GridData.Clear();
            for (int y = MapBounds.Position.Y; y < MapBounds.Position.Y + MapBounds.Size.Y; y++)
            {
                for (int x = MapBounds.Position.X; x < MapBounds.Position.X + MapBounds.Size.X; x++)
                {
                    var cell = new GridCell(x, y);
                    cell.TerrainType = 0;
                    GridData[new Vector2I(x, y)] = cell;
                }
            }
        }

        /// <summary>
        /// 根据当前 GridData 中所有存在格子的坐标重新计算 MapBounds。
        /// </summary>
        public void RecalculateMapBounds()
        {
            if (GridData.Count == 0)
            {
                MapBounds = new Rect2I(0, 0, 50, 50);
                return;
            }

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var pos in GridData.Keys)
            {
                if (pos.X < minX) minX = pos.X;
                if (pos.X > maxX) maxX = pos.X;
                if (pos.Y < minY) minY = pos.Y;
                if (pos.Y > maxY) maxY = pos.Y;
            }
            MapBounds = new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public override void _Draw()
        {
            // 地图范围外灰色 + 地形墙填充 + 地形颜色覆盖层：全部由 GPU shader 渲染
            // (grid_overlay.gdshader 通过 terrain_mask RGBA8 纹理 + outside_map_color 处理)
            // 不再需要 CPU 端 DrawOutsideMapGray / DrawTerrainWalls / DrawWalkableOverlay

            // 文本覆盖层保留在 CPU 端（GPU 难以高效渲染多语言文本）
            if (IsEditMode)
                DrawTerrainLabels();
            if (ShowGridCoords)
                DrawGridCoords();
            if (ShowCellUids)
                DrawCellUids();
        }

        private void DrawGrid()
        {
            // 网格线由 GPU shader 渲染（GridShaderOverlay），此方法保留为空
        }

        public (float lineWidthWorld, Color lineColor) ComputeGridLineRenderStylePublic(float cameraZoom)
        {
            return ComputeGridLineRenderStyle(cameraZoom);
        }

        private (float lineWidthWorld, Color lineColor) ComputeGridLineRenderStyle(float cameraZoom)
        {
            float targetScreenLineWidth;
            if (IsEditMode)
            {
                // 编辑模式下使用固定屏幕线宽，保证任何 zoom 下线宽一致且可见
                // 不再随距离变粗（自适应校准/自动线宽在编辑模式下不生效）
                targetScreenLineWidth = 2.0f;
            }
            else
            {
                targetScreenLineWidth = GetTargetScreenLineWidth(cameraZoom);
                // 自适应模式下，上限使用参考点最大值，避免硬编码 2px 截断用户设置
                float maxWidth = AdaptiveCalibrationEnabled
                    ? Mathf.Max(MaxScreenLineWidth, Mathf.Max(RefWidthA, RefWidthB))
                    : MaxScreenLineWidth;
                targetScreenLineWidth = Mathf.Clamp(targetScreenLineWidth, MinScreenLineWidth, maxWidth);
            }

            float alphaScale = 1.0f;
            float drawScreenLineWidth = targetScreenLineWidth;

            // 小于 1px 时改用 1px 几何线 + alpha 模拟细线，稳定性比真实亚像素矩形更好。
            if (targetScreenLineWidth < 1.0f)
            {
                drawScreenLineWidth = 1.0f;
                alphaScale = Mathf.Clamp(targetScreenLineWidth, 0.35f, 1.0f);
            }

            float lineWidthWorld = drawScreenLineWidth / cameraZoom;
            // 只保留下限，不限制上限。远距离时 world width 必须足够大才能保证屏幕线宽可见。
            // 之前的 GridSize * 0.3f 上限在 zoom 很小时会截断 world width，导致屏幕线宽 < 1px。
            lineWidthWorld = Mathf.Max(lineWidthWorld, 0.01f);

            var lineColor = IsEditMode ? new Color(1.0f, 1.0f, 1.0f, 1.0f) : LineColor;
            lineColor.A *= alphaScale;
            return (lineWidthWorld, lineColor);
        }

        private float GetTargetScreenLineWidth(float cameraZoom)
        {
            if (_previewLineWidth > 0.0f)
                return _previewLineWidth;

            if (AdaptiveCalibrationEnabled)
                return GetAdaptiveLineWidth(cameraZoom);

            if (AutoLineWidth)
                return Mathf.Max(LineWidthScale, MinScreenLineWidth);

            return Mathf.Max(LineWidth, MinScreenLineWidth);
        }

        private float GetCameraZoom()
        {
            var viewport = GetViewport();
            if (viewport != null)
            {
                var camera = viewport.GetCamera2D();
                if (camera != null)
                    return camera.Zoom.X;
            }
            return 1.0f;
        }

        /// <summary>
        /// 无条件绘制地形墙(9)的深灰色填充，普通模式和编辑模式下都显示，
        /// 使地形墙与地图边界外区域视觉一致。
        /// </summary>
        // ============ 地形标签显示 ============

        private Font? _terrainLabelFont;
        private int _terrainLabelFontSize = 14;

        private void DrawTerrainLabels()
        {
            var cameraZoom = GetCameraZoom();
            if (cameraZoom < 0.5f) return;

            foreach (var cell in GridData.Values)
            {
                if (cell.TerrainType == 0)
                    continue;

                var name = cell.GetTerrainName();
                if (string.IsNullOrEmpty(name)) continue;

                var x = cell.Pos.X;
                var y = cell.Pos.Y;

                // 半透明白色背景
                var bgRect = new Rect2(new Vector2(x * GridSize + 4, y * GridSize + 4), new Vector2(GridSize - 8, GridSize - 8));
                DrawRect(bgRect, new Color(1, 1, 1, 0.25f), true);

                var fontSize = Mathf.Min(16, (int)(GridSize * 0.4f));
                var textSize = _terrainLabelFont.GetStringSize(name, fontSize: fontSize);
                var pos = new Vector2(
                    x * GridSize + (GridSize - textSize.X) / 2,
                    y * GridSize + (GridSize + textSize.Y) / 2
                );

                var outlineColor = Colors.Black;
                var textColor = Colors.White;

                DrawString(_terrainLabelFont, pos + new Vector2(-1, 0), name, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: outlineColor);
                DrawString(_terrainLabelFont, pos + new Vector2(1, 0), name, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: outlineColor);
                DrawString(_terrainLabelFont, pos + new Vector2(0, -1), name, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: outlineColor);
                DrawString(_terrainLabelFont, pos + new Vector2(0, 1), name, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: outlineColor);
                DrawString(_terrainLabelFont, pos, name, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: textColor);
            }
        }

        // ============ 坐标转换 ============

        public Vector2 GridToWorld(Vector2I gridPos)
        {
            var localX = gridPos.X * GridSize + GridSize / 2.0f;
            var localY = gridPos.Y * GridSize + GridSize / 2.0f;
            return new Vector2(localX, localY);
        }

        public Vector2I WorldToGrid(Vector2 worldPos)
        {
            var gridX = Mathf.FloorToInt(worldPos.X / GridSize);
            var gridY = Mathf.FloorToInt(worldPos.Y / GridSize);
            return new Vector2I(gridX, gridY);
        }

        public bool IsInBounds(Vector2I gridPos)
        {
            return GridData.ContainsKey(gridPos);
        }

        // ============ 行走检查 ============

        // 被宝箱占据的格子（未开的宝箱阻挡移动）
        private HashSet<Vector2I> _blockedByChest = new();
        // 被建筑 footprint 阻塞的格子（由 decoration 锚点展开）
        private HashSet<Vector2I> _blockedByDecoration = new();

        public void BlockCell(Vector2I pos) => _blockedByChest.Add(pos);
        public void UnblockCell(Vector2I pos) => _blockedByChest.Remove(pos);
        public bool IsBlockedByChest(Vector2I pos) => _blockedByChest.Contains(pos);

        /// <summary>
        /// 根据当前 GridData 中的 decoration 锚点重新计算 footprint 阻塞集合。
        /// 多格建筑只会在锚点格子保存 decoration，但占地范围内所有格子都应被阻塞。
        /// </summary>
        public void RebuildBlockedByDecoration()
        {
            _blockedByDecoration.Clear();
            foreach (var cell in GridData.Values)
            {
                if (cell.DecorationType == 0) continue;
                if (!BlocksMovementByProfile(cell.DecorationType)) continue;

                var (sizeX, sizeY) = GetDecorationSize(cell.DecorationType);
                for (int dy = 0; dy < sizeY; dy++)
                {
                    for (int dx = 0; dx < sizeX; dx++)
                    {
                        var pos = new Vector2I(cell.Pos.X + dx, cell.Pos.Y + dy);
                        if (GridData.ContainsKey(pos))
                            _blockedByDecoration.Add(pos);
                    }
                }
            }
        }

        private static (int sizeX, int sizeY) GetDecorationSize(int profileId)
        {
            var profile = EntityProfileManager.Instance?.GetProfile(profileId);
            var app = profile?.GetData<AppearanceData>("appearance");
            return (app?.SizeX > 0 ? app.SizeX : 1, app?.SizeY > 0 ? app.SizeY : 1);
        }

        public bool IsWalkable(Vector2I gridPos)
        {
            if (!IsInBounds(gridPos))
            {
#if DEBUG
                GD.Print($"[IsWalkable] {gridPos} 超出地图范围 (MW={MapWidth}, MH={MapHeight})");
#endif
                return false;
            }
            if (_blockedByChest.Contains(gridPos))
            {
#if DEBUG
                GD.Print($"[IsWalkable] {gridPos} 被宝箱阻挡");
#endif
                return false;
            }
            var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (mm != null && mm.IsBlockedByMonster(gridPos))
            {
#if DEBUG
                GD.Print($"[IsWalkable] {gridPos} 被怪物阻挡");
#endif
                return false;
            }
            var nm = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            if (nm != null && nm.IsBlockedByNpc(gridPos))
            {
#if DEBUG
                GD.Print($"[IsWalkable] {gridPos} 被NPC阻挡");
#endif
                return false;
            }
            var cell = GridData[gridPos];
            if (_blockedByDecoration.Contains(gridPos))
            {
#if DEBUG
                GD.Print($"[IsWalkable] {gridPos} 被装饰摆件 footprint 阻挡");
#endif
                return false;
            }

            var walkable = cell.TerrainConfig?.Walkable ?? true;
#if DEBUG
            if (!walkable)
            {
                GD.Print($"[IsWalkable] {gridPos} 地形不可行走: terrain={cell.TerrainType}, configNull={cell.TerrainConfig==null}");
            }
            else
            {
                GD.Print($"[IsWalkable] {gridPos} 可行走: terrain={cell.TerrainType}, configNull={cell.TerrainConfig==null}");
            }
#endif
            return walkable;
        }

        /// <summary>
        /// 通过 EntityProfileManager 判断指定 Decoration Profile 是否阻塞移动。
        /// </summary>
        private static bool BlocksMovementByProfile(int profileId)
        {
            // 兼容旧 decoration type（1=房舍，2=商店），转换为 build_cfg_id
            if (profileId == BuildingType.House)
                profileId = BuildingType.GetConfigBaseId(BuildingType.House) + 1;
            else if (profileId == BuildingType.Shop)
                profileId = BuildingType.GetConfigBaseId(BuildingType.Shop);

            var profile = EntityProfileManager.Instance?.GetProfile(profileId);
            if (profile == null || profile.EntityType != "decoration")
                return false;

            var obstacle = profile.GetData<ObstacleData>("obstacle");
            return obstacle != null && obstacle.BlockMovement && !profile.IsComponentDisabled("obstacle");
        }

        public bool IsCellVisible(Vector2I gridPos)
        {
            // 所有地图范围内的格子都可见
            return IsInBounds(gridPos);
        }

        public GridCell GetCell(Vector2I gridPos)
        {
            GridData.TryGetValue(gridPos, out var cell);
            return cell;
        }

        public void SetCell(Vector2I gridPos, GridCell cellData)
        {
            if (!GridData.ContainsKey(gridPos))
                return;
            cellData.CopyTo(GridData[gridPos]);
            QueueRedraw();
        }

        // ============ 设置方法 ============

        private void OnViewportResized()
        {
            if (ResponsiveMode)
                UpdateResponsiveGridSize();
        }

        public void UpdateResponsiveGridSize()
        {
            // 根据视口大小计算格子大小，确保始终显示固定数量的格子
            var viewportSize = GetViewportRect().Size;

            // 根据屏幕宽高比自动计算纵向格子数（格子是正方形）
            var visibleGridsY = VisibleGridsX * (viewportSize.Y / viewportSize.X);

            // 根据视口大小和目标显示格子数计算格子大小
            var targetSizeX = viewportSize.X / VisibleGridsX;
            var targetSizeY = viewportSize.Y / visibleGridsY;

            // 取较小值确保完整显示（保持正方形格子）
            var targetSize = Mathf.Min(targetSizeX, targetSizeY);

            // 限制在最小/最大范围内
            var newGridSize = Mathf.Clamp((int)targetSize, MinGridSize, MaxGridSize);

            if (newGridSize != GridSize)
            {
            #if DEBUG
            GD.Print($"[GridManager] 响应式调整: 视口={viewportSize}, 新格子大小={newGridSize}");
            #endif
                SetGridSize(newGridSize);

                // 调整相机确保覆盖目标格子数
                UpdateCameraForResponsive();
            }
        }

        private void UpdateCameraForResponsive()
        {
            // 调整相机确保正确显示目标数量的格子
            var camera = GetTree().GetFirstNodeInGroup("camera") as CameraController;
            if (camera == null)
                return;

            var viewportSize = GetViewportRect().Size;

            // 根据屏幕宽高比自动计算纵向格子数
            var visibleGridsY = VisibleGridsX * (viewportSize.Y / viewportSize.X);

            // 计算需要的 zoom 来显示目标格子数
            var neededZoomX = viewportSize.X / (VisibleGridsX * GridSize);
            var neededZoomY = viewportSize.Y / (visibleGridsY * GridSize);

            // 取较小值确保完整显示
            var targetZoom = Mathf.Min(neededZoomX, neededZoomY);

            // 应用 zoom（不使用平滑过渡，直接设置）
            camera.Zoom = new Vector2(targetZoom, targetZoom);

            // 如果玩家存在，确保玩家在视野中心
            var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            if (player != null)
                camera.Position = player.Position;
        }

        private bool _isViewportResizedConnected = false;

        public void SetResponsiveMode(bool enabled)
        {
            ResponsiveMode = enabled;
            if (enabled)
            {
                UpdateResponsiveGridSize();
                if (!_isViewportResizedConnected)
                {
                    GetTree().Root.SizeChanged += OnViewportResized;
                    _isViewportResizedConnected = true;
                }
            }
            else
            {
                if (_isViewportResizedConnected)
                {
                    GetTree().Root.SizeChanged -= OnViewportResized;
                    _isViewportResizedConnected = false;
                }
            }
        }

        public void SetLineWidthScale(float maxWidth)
        {
            // line_width_scale 现在表示最大线宽（像素）
            LineWidthScale = Mathf.Clamp(maxWidth, 0.1f, 20.0f);
            QueueRedraw();
        }

        public void SetGridAntiAliasSoftness(float value)
        {
            GridAntiAliasSoftness = GridOverlayAntiAliasSoftnessPolicy.Clamp(value);
            QueueRedraw();
        }

        public float GetGridAntiAliasSoftness() => GridAntiAliasSoftness;

        public void SetFixedWorldLineWidth(float width)
        {
            LineWidth = Mathf.Clamp(width, 0.01f, 20.0f);
            QueueRedraw();
        }

        public float GetLineWidthScale() => LineWidthScale;

        public void SetAutoLineWidth(bool enabled)
        {
            AutoLineWidth = enabled;
            QueueRedraw();
        }

        public void SetLineWidthLimits(float minWidth, float maxWidth)
        {
            MinScreenLineWidth = minWidth;
            MaxScreenLineWidth = maxWidth;
            QueueRedraw();
        }

        public void SetLineWidthCalibration(float zoomA, float widthA, float zoomB, float widthB)
        {
            RefZoomA = zoomA;
            RefWidthA = widthA;
            RefZoomB = zoomB;
            RefWidthB = widthB;
            QueueRedraw();
        }

        public void SetAdaptiveCalibrationEnabled(bool enabled)
        {
            AdaptiveCalibrationEnabled = enabled;
            _lastCameraZoom = GetCameraZoom();
            QueueRedraw();
        }

        public GridLineWidthMode GetGridLineWidthMode()
        {
            if (AdaptiveCalibrationEnabled)
                return GridLineWidthMode.AdaptiveCalibration;
            if (AutoLineWidth)
                return GridLineWidthMode.FixedScreen;
            return GridLineWidthMode.FixedWorld;
        }

        public void SetGridLineWidthMode(GridLineWidthMode mode)
        {
            switch (mode)
            {
                case GridLineWidthMode.FixedWorld:
                    SetAdaptiveCalibrationEnabled(false);
                    SetAutoLineWidth(false);
                    break;
                case GridLineWidthMode.FixedScreen:
                    SetAdaptiveCalibrationEnabled(false);
                    SetAutoLineWidth(true);
                    break;
                case GridLineWidthMode.AdaptiveCalibration:
                    SetAutoLineWidth(false);
                    SetAdaptiveCalibrationEnabled(true);
                    break;
            }
            QueueRedraw();
        }

        public GridSizeMode GetGridSizeMode()
        {
            return ResponsiveMode ? GridSizeMode.ResponsiveVisibleCount : GridSizeMode.Manual;
        }

        public void SetGridSizeMode(GridSizeMode mode)
        {
            SetResponsiveMode(mode == GridSizeMode.ResponsiveVisibleCount);
        }

        public void SetPreviewLineWidth(float width)
        {
            _previewLineWidth = width;
            QueueRedraw();
        }

        public void ClearPreviewLineWidth()
        {
            _previewLineWidth = -1.0f;
            QueueRedraw();
        }

        public Dictionary GetLineWidthCalibration()
        {
            return new Dictionary
            {
                ["zoom_a"] = RefZoomA,
                ["width_a"] = RefWidthA,
                ["zoom_b"] = RefZoomB,
                ["width_b"] = RefWidthB
            };
        }

        private float GetAdaptiveLineWidth(float zoom)
        {
            const float EPSILON = 0.001f;
            zoom = Mathf.Max(zoom, 0.01f);

            if (Mathf.Abs(RefZoomB - RefZoomA) < EPSILON)
            {
                var avgWidth = (RefWidthA + RefWidthB) / 2.0f;
                return avgWidth;
            }

            float lowZoom, lowWidth, highZoom, highWidth;

            if (RefZoomA < RefZoomB)
            {
                lowZoom = RefZoomA;
                lowWidth = RefWidthA;
                highZoom = RefZoomB;
                highWidth = RefWidthB;
            }
            else
            {
                lowZoom = RefZoomB;
                lowWidth = RefWidthB;
                highZoom = RefZoomA;
                highWidth = RefWidthA;
            }

            float logLow = Mathf.Log(Mathf.Max(lowZoom, 0.01f));
            float logHigh = Mathf.Log(Mathf.Max(highZoom, 0.01f));
            float logZoom = Mathf.Log(zoom);
            float t = Mathf.InverseLerp(logLow, logHigh, logZoom);
            t = t * t * (3.0f - 2.0f * t);
            return Mathf.Lerp(lowWidth, highWidth, t);
        }

        public void SetGridSize(int newSize)
        {
            // 防止无效值
            if (newSize < MinGridSize)
            {
                GD.PushError($"[GridManager] Invalid grid size: {newSize}, using minimum: {MinGridSize}");
                newSize = MinGridSize;
            }
            if (newSize > MaxGridSize)
            {
                GD.PushError($"[GridManager] Invalid grid size: {newSize}, using maximum: {MaxGridSize}");
                newSize = MaxGridSize;
            }

            GridSize = newSize;
#if DEBUG
            GD.Print($"[GridManager] GridSize set to: {GridSize}");
#endif
            QueueRedraw();

            // 作为单一数据源，自动同步玩家、怪物和NPC
            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            player?.SetGridSize(newSize);

            var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            mm?.SetGridSize(newSize);

            var nm = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            nm?.SetGridSize(newSize);

            UpdateGridShaderOverlay();
            SyncBackgroundSize();
        }

        public void SyncBackgroundSize()
        {
            if (_background == null)
            {
                _background = GetParent()?.GetNodeOrNull<ColorRect>("Background");
                if (_background == null) return;
            }
            _background.Size = new Vector2(MapBounds.Size.X * GridSize, MapBounds.Size.Y * GridSize);
            _background.Position = new Vector2(MapBounds.Position.X * GridSize, MapBounds.Position.Y * GridSize);
        }

        public void SetLineBrightness(float brightness)
        {
            LineColor = new Color(brightness, brightness, brightness, 1.0f);
            QueueRedraw();
        }

        public void SetEditMode(bool enabled)
        {
            IsEditMode = enabled;
            // 地形颜色/墙由 GPU shader 始终渲染，无需额外开关
            UpdateTerrainMask();
            UpdateGridShaderOverlay();
            QueueRedraw();
        }

        /// <summary>
        /// 刷新地图外部灰色区域的可见性（根据 ShowOutsideMapGray 开关更新 shader）
        /// </summary>
        public void RefreshOutsideMapVisibility()
        {
            if (_gridShaderOverlay == null) return;
            var color = ShowOutsideMapGray ? OutsideMapColor : Colors.Transparent;
            _gridShaderOverlay.UpdateOutsideMapColor(color);
        }

        /// <summary>
        /// 通知地形数据已变化，更新 Shader 遮罩纹理。
        /// 由 MapEditor 在修改地形后调用。
        /// </summary>
        public void NotifyTerrainChanged()
        {
            UpdateTerrainMask();
            QueueRedraw();
        }



        /// <summary>
        /// 根据当前 GridData 生成 RGBA8 地形遮罩纹理并更新到 Shader。
        /// R 通道：0=地形墙(不绘制网格线，显示灰色填充)，255=普通格子
        /// GBA 通道：地形配置颜色（RGB），地形墙使用 OutsideMapColor
        /// 同时生成 water_mask：R=1 表示水域，R=0 表示非水域。
        /// </summary>
        private void UpdateTerrainMask()
        {
            if (_gridShaderOverlay == null || GridData.Count == 0)
            {
#if DEBUG
                GD.Print($"[GridManager] UpdateTerrainMask: 跳过, _gridShaderOverlay=null?{_gridShaderOverlay==null}, GridData.Count={GridData.Count}");
#endif
                return;
            }

            var bounds = MapBounds;
            var image = Image.CreateEmpty(bounds.Size.X, bounds.Size.Y, false, Image.Format.Rgba8);
            var waterImage = Image.CreateEmpty(bounds.Size.X, bounds.Size.Y, false, Image.Format.Rgba8);

            for (int y = 0; y < bounds.Size.Y; y++)
            {
                for (int x = 0; x < bounds.Size.X; x++)
                {
                    var logicalPos = new Vector2I(bounds.Position.X + x, bounds.Position.Y + y);
                    byte maskValue;
                    float r, g, b;
                    bool isWater = false;

                    if (GridData.TryGetValue(logicalPos, out var cell))
                    {
                        if (cell.TerrainType == 9)
                        {
                            // 地形墙：mask=0(灰色填充)，颜色=OutsideMapColor
                            maskValue = 0;
                            r = OutsideMapColor.R;
                            g = OutsideMapColor.G;
                            b = OutsideMapColor.B;
                        }
                        else if (cell.TerrainType == 10)
                        {
                            // 空气墙：mask=255(显示网格线)，颜色=淡红色
                            maskValue = 255;
                            r = 1.0f;
                            g = 0.0f;
                            b = 0.0f;
                        }
                        else
                        {
                            // 普通格子：mask=255(显示网格线)
                            maskValue = 255;
                            var cfg = cell.TerrainConfig ?? TerrainConfigUtil.Get(cell.TerrainType);
                            // 只有 ColorA > 0 时才渲染地形颜色背景（ColorA=0 表示透明，走纯网格线渲染）
                            if (cfg != null && cfg.ColorA > 0 && (cfg.ColorR > 0 || cfg.ColorG > 0 || cfg.ColorB > 0))
                            {
                                r = cfg.ColorR / 255f;
                                g = cfg.ColorG / 255f;
                                b = cfg.ColorB / 255f;
                            }
                            else
                            {
                                r = 0f;
                                g = 0f;
                                b = 0f;
                            }

                            isWater = cell.TerrainType == 1;
                        }
                    }
                    else
                    {
                        // 不存在的格子：按地图外处理
                        maskValue = 0;
                        r = OutsideMapColor.R;
                        g = OutsideMapColor.G;
                        b = OutsideMapColor.B;
                    }

                    image.SetPixel(x, y, new Color(maskValue / 255f, r, g, b));
                    waterImage.SetPixel(x, y, isWater ? new Color(1.0f, 0.0f, 0.0f, 0.0f) : new Color(0.0f, 0.0f, 0.0f, 0.0f));
                }
            }

            // 复用同一个 ImageTexture 实例，避免 Godot 渲染服务器因纹理引用变化导致缓存/批次问题
            if (_terrainMaskTexture != null &&
                _terrainMaskTexture.GetWidth() == bounds.Size.X &&
                _terrainMaskTexture.GetHeight() == bounds.Size.Y)
            {
                _terrainMaskTexture.Update(image);
            }
            else
            {
                _terrainMaskTexture = ImageTexture.CreateFromImage(image);
            }

            if (_waterMaskTexture != null &&
                _waterMaskTexture.GetWidth() == bounds.Size.X &&
                _waterMaskTexture.GetHeight() == bounds.Size.Y)
            {
                _waterMaskTexture.Update(waterImage);
            }
            else
            {
                _waterMaskTexture = ImageTexture.CreateFromImage(waterImage);
            }

            _gridShaderOverlay.UpdateTerrainMask(_terrainMaskTexture, bounds.Size.X, bounds.Size.Y);
            _gridShaderOverlay.UpdateWaterMask(_waterMaskTexture, bounds.Size.X, bounds.Size.Y);
            _gridShaderOverlay.UpdateOutsideMapColor(ShowOutsideMapGray ? OutsideMapColor : Colors.Transparent);
#if DEBUG
            GD.Print($"[GridManager] UpdateTerrainMask: 已更新 terrain/water mask {bounds.Size.X}x{bounds.Size.Y}, bounds={bounds}");
#endif
        }

        public void SetShowGridCoords(bool show)
        {
            ShowGridCoords = show;
            QueueRedraw();
        }

        public void SetShowCellUids(bool show)
        {
            ShowCellUids = show;
            QueueRedraw();
        }

        private void DrawGridCoords()
        {
            // 绘制每个格子的逻辑坐标
            var font = _terrainLabelFont ?? ThemeDB.FallbackFont;
            var fontSize = Mathf.Max(8, GridSize / 6);  // 根据格子大小动态调整字号
            var textColor = new Color(0.8f, 0.8f, 0.8f, 0.7f);  // 浅灰色

            foreach (var cell in GridData.Values)
            {
                var worldPos = GridToWorld(cell.Pos);
                var text = $"x:{cell.Pos.X}\ny:{cell.Pos.Y}";

                // 计算文字位置（居中）
                var textSize = font.GetMultilineStringSize(text, HorizontalAlignment.Center, -1, fontSize);
                var textPos = worldPos - new Vector2(textSize.X / 2, textSize.Y / 2);

                // 绘制文字
                DrawMultilineString(font, textPos, text, HorizontalAlignment.Center, -1, fontSize, (int)(textSize.Y + 2), textColor);
            }
        }

        private void DrawCellUids()
        {
            // 绘制每个格子的 UID（唯一标识符，创建时生成，永不改变）
            var font = _terrainLabelFont ?? ThemeDB.FallbackFont;
            var fontSize = Mathf.Max(8, GridSize / 6);
            var textColor = new Color(1.0f, 0.9f, 0.3f, 0.85f);  // 金黄色，醒目但不过度遮挡

            foreach (var cell in GridData.Values)
            {
                if (string.IsNullOrEmpty(cell.Uid)) continue;

                var worldPos = GridToWorld(cell.Pos);

                // 计算文字位置（居中）
                var textSize = font.GetStringSize(cell.Uid, fontSize: fontSize);
                var textPos = worldPos - new Vector2(textSize.X / 2, textSize.Y / 2);

                // 绘制文字（带黑色描边增强可读性）
                var outlineColor = Colors.Black;
                DrawString(font, textPos + new Vector2(-1, 0), cell.Uid, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: outlineColor);
                DrawString(font, textPos + new Vector2(1, 0), cell.Uid, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: outlineColor);
                DrawString(font, textPos + new Vector2(0, -1), cell.Uid, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: outlineColor);
                DrawString(font, textPos + new Vector2(0, 1), cell.Uid, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: outlineColor);
                DrawString(font, textPos, cell.Uid, HorizontalAlignment.Left, width: -1, fontSize: fontSize, modulate: textColor);
            }
        }

        // ============ 地图操作 ============

        public Error SaveCurrentMap()
        {
            var spawn = MapDataManager.FindSpawnPointFromGridData(GridData, _loadedSpawn);
            return MapDataManager.SaveMapToJson(CurrentMapName, GridData, CurrentMapName, MapBounds, spawn);
        }

        /// <summary>
        /// 扩展当前地图：只新增给定格子的集合，默认填充普通地形。
        /// 支持产生负坐标，会同步更新 MapBounds。
        /// </summary>
        public Error ExtendMap(IEnumerable<Vector2I> cellsToAdd)
        {
            int addedCount = 0;
            foreach (var logicalPos in cellsToAdd)
            {
                if (GridData.ContainsKey(logicalPos))
                    continue;

                var newCell = new GridCell(logicalPos.X, logicalPos.Y);
                newCell.TerrainType = 0;
                newCell.TerrainConfig = TerrainConfigUtil.Get(0);
                GridData[logicalPos] = newCell;
                addedCount++;
            }

            if (addedCount == 0)
            {
                GD.PushWarning("[GridManager.ExtendMap] 没有新格子可添加");
                return Error.AlreadyExists;
            }

#if DEBUG
            GD.Print($"[GridManager.ExtendMap] 实际新增 {addedCount} 个格子");
#endif
            RecalculateMapBounds();
            SaveMapBoundsToConfig();
            SaveCurrentMap();

            // 刷新渲染与持久化
            UpdateGridShaderOverlay();
            _gridShaderOverlay?.UpdateTerrainMask(null, 0, 0);
            _gridShaderOverlay?.UpdateWaterMask(null, 0, 0);
            UpdateTerrainMask();
            SyncBackgroundSize();
            QueueRedraw();

#if DEBUG
            GD.Print($"[GridManager.ExtendMap] 已新增 {addedCount} 个格子, 当前格子数={GridData.Count}, bounds={MapBounds}");
#endif
            return Error.Ok;
        }

        /// <summary>
        /// 兼容旧接口：传入矩形选区，矩形内所有不在 GridData 中的格子都会被添加。
        /// </summary>
        public Error ExtendMap(Rect2I selectionBounds)
        {
            if (selectionBounds.Size.X <= 0 || selectionBounds.Size.Y <= 0)
            {
                GD.PushError("[GridManager.ExtendMap] 选区尺寸无效");
                return Error.InvalidParameter;
            }

#if DEBUG
            GD.Print($"[GridManager.ExtendMap] 当前格子数={GridData.Count}, 选区={selectionBounds}");
#endif

            var cells = new List<Vector2I>();
            for (int y = selectionBounds.Position.Y; y < selectionBounds.Position.Y + selectionBounds.Size.Y; y++)
            {
                for (int x = selectionBounds.Position.X; x < selectionBounds.Position.X + selectionBounds.Size.X; x++)
                {
                    cells.Add(new Vector2I(x, y));
                }
            }
            return ExtendMap(cells);
        }

        /// <summary>将当前 MapBounds 保存到 map.json</summary>
        public void SaveMapBoundsToConfig()
        {
            SaveCurrentMap();
        }

        public bool LoadMap(string mapName)
        {
#if DEBUG
            GD.Print($"[GridManager] LoadMap: 请求加载 '{mapName}'");
#endif
            // 诊断：替换前统计 terrain 分布
            var beforeStats = GetTerrainStats();
#if DEBUG
            GD.Print($"[GridManager] LoadMap: 替换前 terrain 分布={beforeStats}");
#endif

            var loadedData = MapDataManager.LoadMapFromJson(mapName, out var loadedBounds, out _, out _);
#if DEBUG
            GD.Print($"[GridManager] LoadMap: loadedData.Count={loadedData.Count}");
#endif
            if (loadedData.Count > 0 || FileAccess.FileExists(MapDataManager.MapsFolder + mapName + "/" + MapDataManager.JsonFilename))
            {
                GridData = loadedData;
                MapBounds = loadedBounds;
                RecalculateMapBounds();
                CurrentMapName = mapName;

                UpdateGridShaderOverlay();
                // 先重置 terrain_mask/water_mask，强制 Godot 渲染服务器解除旧纹理绑定
                _gridShaderOverlay?.UpdateTerrainMask(null, 0, 0);
                _gridShaderOverlay?.UpdateWaterMask(null, 0, 0);
                UpdateTerrainMask();
                SyncBackgroundSize();
                SyncDecorations();
                QueueRedraw();
                var afterStats = GetTerrainStats();
                #if DEBUG
            GD.Print($"[GridManager] LoadMap: 成功加载 '{mapName}' 格子数={GridData.Count}, bounds={MapBounds}, 替换后 terrain 分布={afterStats}");
            #endif
                return true;
            }
            GD.PrintErr($"[GridManager] LoadMap: 地图 '{mapName}' 不存在");
            return false;
        }

        /// <summary>
        /// 编辑器插件专用：灌入地图数据并刷新地形/网格渲染，但<b>不</b>生成 MapDecoration 节点
        /// （避免编辑器下触发运行时副作用）。装饰由插件的绘制层自行渲染。
        /// 普通运行时加载请继续使用 <see cref="LoadMap"/>。
        /// </summary>
        public void ApplyLoadedGridData(System.Collections.Generic.Dictionary<Vector2I, GridCell> loadedData, Rect2I loadedBounds)
        {
            GridData = loadedData;
            MapBounds = loadedBounds;
            RecalculateMapBounds();
            UpdateGridShaderOverlay();
            _gridShaderOverlay?.UpdateTerrainMask(null, 0, 0);
            _gridShaderOverlay?.UpdateWaterMask(null, 0, 0);
            UpdateTerrainMask();
            SyncBackgroundSize();
            QueueRedraw();
        }

        /// <summary>
        /// 获取当前 GridData 的 terrain 类型分布统计，用于诊断
        /// </summary>
        private string GetTerrainStats()
        {
            if (GridData.Count == 0) return "(空)";
            var stats = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var cell in GridData.Values)
            {
                var t = cell.TerrainType;
                if (!stats.ContainsKey(t)) stats[t] = 0;
                stats[t]++;
            }
            var parts = new List<string>();
            foreach (var kvp in stats.OrderBy(kv => kv.Key))
                parts.Add($"T{kvp.Key}={kvp.Value}");
            return string.Join(", ", parts);
        }

        public Error ExportJson(string exportPath)
        {
            return MapDataManager.ExportJson(CurrentMapName, exportPath);
        }

        public Error ImportJson(string importPath)
        {
            var err = MapDataManager.ImportJson(importPath, CurrentMapName);
            if (err == Error.Ok)
            {
                // 重新加载
                LoadMap(CurrentMapName);
            }
            return err;
        }

    }
}
