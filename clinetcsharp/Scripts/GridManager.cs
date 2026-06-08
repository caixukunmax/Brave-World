using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 网格管理器
    /// </summary>
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

        [Export] public int GridSize { get; set; } = 64;
        [Export] public int MapWidth { get; set; } = 50;
        [Export] public int MapHeight { get; set; } = 50;
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

        public List<List<GridCell>> GridData { get; set; } = new();
        public string CurrentMapName { get; set; } = "新手村";

        // 响应式布局设置
        [Export] public bool ResponsiveMode { get; set; } = false;  // 是否启用响应式格子大小
        [Export] public float VisibleGridsX { get; set; } = 5.0f;     // 屏幕横向显示的格子数（支持小数，如5.5）
        [Export] public int MinGridSize { get; set; } = 32;        // 最小格子大小（防止太小）
        [Export] public int MaxGridSize { get; set; } = 256;       // 最大格子大小（防止太大）

        // 编辑模式
        public bool IsEditMode { get; set; } = false;
        public bool ShowWalkableOverlay { get; set; } = false;
        public bool ShowGridCoords { get; set; } = false;  // 显示格子坐标
        public bool ShowTerrainLabels { get; set; } = false;  // 显示地形名称标签

        // 地图范围外灰色显示设置
        [Export] public bool ShowOutsideMapGray { get; set; } = true;
        [Export] public Color OutsideMapColor { get; set; } = new Color(0.15f, 0.15f, 0.15f, 1.0f);

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

        public override void _Ready()
        {
            // 初始化地形标签字体
            _terrainLabelFont = ThemeDB.Singleton?.FallbackFont;
            if (_terrainLabelFont == null)
            {
                var sysFont = new SystemFont();
                sysFont.FontNames = new string[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "WenQuanYi Zen Hei" };
                _terrainLabelFont = sysFont;
            }

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

            EnsureGridShaderOverlay();
            UpdateGridShaderOverlay();
            UpdateTerrainMask();

            QueueRedraw();
        }

        public override void _Process(double _delta)
        {
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

        private void UpdateGridShaderOverlay()
        {
            EnsureGridShaderOverlay();
            if (_gridShaderOverlay == null)
                return;

            var cameraZoom = Mathf.Clamp(GetCameraZoom(), GridRenderMinZoom, GridRenderMaxZoom);
            var (lineWidthWorld, lineColor) = ComputeGridLineRenderStyle(cameraZoom);
            float screenLineWidth = lineWidthWorld * cameraZoom;

            if (IsEditMode)
            {
                if (screenLineWidth < 1.5f)
                    screenLineWidth = 1.5f;
                lineColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            }

            _gridShaderOverlay.UpdateOverlay(
                GridSize,
                MapWidth,
                MapHeight,
                screenLineWidth,
                lineColor,
                GridAntiAliasSoftness);

            _gridShaderOverlay.UpdateOutsideMapColor(OutsideMapColor);
        }

        private void LoadMapData()
        {
            // 尝试从CSV加载
            var loadedData = MapDataManager.LoadMapFromCsv(CurrentMapName);

            if (loadedData.Count > 0)
            {
                GridData = loadedData;
                MapHeight = GridData.Count;
                MapWidth = GridData.Count > 0 ? GridData[0].Count : 50;
                GD.Print($"[GridManager] 加载地图: {CurrentMapName} {MapWidth}x{MapHeight}");
            }
            else
            {
                // 创建默认地图
                GD.Print("[GridManager] 创建默认地图数据");
                CreateDefaultGridData();
                // 保存默认地图
                if (!MapDataManager.MapExists(CurrentMapName))
                    MapDataManager.CreateNewMap(CurrentMapName, MapWidth, MapHeight);
                MapDataManager.SaveMapToCsv(CurrentMapName, GridData);
            }

            UpdateTerrainMask();
        }

        private void CreateDefaultGridData()
        {
            GridData.Clear();
            for (int y = 0; y < MapHeight; y++)
            {
                var row = new List<GridCell>();
                for (int x = 0; x < MapWidth; x++)
                {
                    var cell = new GridCell(x, y);
                    cell.TerrainType = 0;
                    row.Add(cell);
                }
                GridData.Add(row);
            }
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
            float targetScreenLineWidth = GetTargetScreenLineWidth(cameraZoom);
            // 自适应模式下，上限使用参考点最大值，避免硬编码 2px 截断用户设置
            float maxWidth = AdaptiveCalibrationEnabled
                ? Mathf.Max(MaxScreenLineWidth, Mathf.Max(RefWidthA, RefWidthB))
                : MaxScreenLineWidth;
            targetScreenLineWidth = Mathf.Clamp(targetScreenLineWidth, MinScreenLineWidth, maxWidth);

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

            var lineColor = LineColor;
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

            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    var cell = GridData[y][x];
                    if (cell.TerrainType == 0)
                        continue;

                    var name = cell.GetTerrainName();
                    if (string.IsNullOrEmpty(name)) continue;

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
        }

        // ============ 坐标转换 ============

        public Vector2 GridToWorld(Vector2I gridPos)
        {
            return new Vector2(gridPos.X * GridSize + GridSize / 2.0f,
                               gridPos.Y * GridSize + GridSize / 2.0f);
        }

        public Vector2I WorldToGrid(Vector2 worldPos)
        {
            return new Vector2I(Mathf.FloorToInt(worldPos.X / GridSize),
                                Mathf.FloorToInt(worldPos.Y / GridSize));
        }

        public bool IsInBounds(Vector2I gridPos)
        {
            return gridPos.X >= 0 && gridPos.X < MapWidth &&
                   gridPos.Y >= 0 && gridPos.Y < MapHeight;
        }

        // ============ 行走检查 ============

        // 被宝箱占据的格子（未开的宝箱阻挡移动）
        private HashSet<Vector2I> _blockedByChest = new();

        public void BlockCell(Vector2I pos) => _blockedByChest.Add(pos);
        public void UnblockCell(Vector2I pos) => _blockedByChest.Remove(pos);
        public bool IsBlockedByChest(Vector2I pos) => _blockedByChest.Contains(pos);

        public bool IsWalkable(Vector2I gridPos)
        {
            if (!IsInBounds(gridPos))
            {
                GD.Print($"[IsWalkable] {gridPos} 超出地图范围 (MW={MapWidth}, MH={MapHeight})");
                return false;
            }
            if (_blockedByChest.Contains(gridPos))
            {
                GD.Print($"[IsWalkable] {gridPos} 被宝箱阻挡");
                return false;
            }
            var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (mm != null && mm.IsBlockedByMonster(gridPos))
            {
                GD.Print($"[IsWalkable] {gridPos} 被怪物阻挡");
                return false;
            }
            var nm = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            if (nm != null && nm.IsBlockedByNpc(gridPos))
            {
                GD.Print($"[IsWalkable] {gridPos} 被NPC阻挡");
                return false;
            }
            var cell = GridData[gridPos.Y][gridPos.X];
            var walkable = cell.TerrainConfig?.Walkable ?? true;
            if (!walkable)
            {
                GD.Print($"[IsWalkable] {gridPos} 地形不可行走: terrain={cell.TerrainType}, configNull={cell.TerrainConfig==null}");
            }
            else
            {
                GD.Print($"[IsWalkable] {gridPos} 可行走: terrain={cell.TerrainType}, configNull={cell.TerrainConfig==null}");
            }
            return walkable;
        }

        public bool IsCellVisible(Vector2I gridPos)
        {
            // 所有地图范围内的格子都可见
            return IsInBounds(gridPos);
        }

        public GridCell GetCell(Vector2I gridPos)
        {
            if (!IsInBounds(gridPos))
                return null;
            return GridData[gridPos.Y][gridPos.X];
        }

        public void SetCell(Vector2I gridPos, GridCell cellData)
        {
            if (!IsInBounds(gridPos))
                return;
            cellData.CopyTo(GridData[gridPos.Y][gridPos.X]);
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
                GD.Print($"[GridManager] 响应式调整: 视口={viewportSize}, 新格子大小={newGridSize}");
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
            GD.Print($"[GridManager] GridSize set to: {GridSize}");
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

        private void SyncBackgroundSize()
        {
            if (_background == null)
            {
                _background = GetParent()?.GetNodeOrNull<ColorRect>("Background");
                if (_background == null) return;
            }
            _background.Size = new Vector2(MapWidth * GridSize, MapHeight * GridSize);
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
        /// 通知地形数据已变化，更新 Shader 遮罩纹理。
        /// 由 MapEditor 在修改地形后调用。
        /// </summary>
        public void NotifyTerrainChanged()
        {
            UpdateTerrainMask();
            QueueRedraw();
        }

        /// <summary>
        /// 根据当前 GridData 生成地形墙遮罩纹理并更新到 Shader。
        /// 遮罩中黑色(0)表示地形墙（不绘制网格线），白色(1)表示普通格子。
        /// </summary>
        /// <summary>
        /// 根据当前 GridData 生成 RGBA8 地形遮罩纹理并更新到 Shader。
        /// R 通道：0=地形墙(不绘制网格线，显示灰色填充)，255=普通格子
        /// GBA 通道：地形配置颜色（RGB），地形墙使用 OutsideMapColor
        /// </summary>
        private void UpdateTerrainMask()
        {
            if (_gridShaderOverlay == null || GridData.Count == 0)
                return;

            var image = Image.CreateEmpty(MapWidth, MapHeight, false, Image.Format.Rgba8);
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    var cell = GridData[y][x];
                    byte maskValue;
                    float r, g, b;

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
                        if (cfg != null && (cfg.ColorR > 0 || cfg.ColorG > 0 || cfg.ColorB > 0))
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
                    }

                    image.SetPixel(x, y, new Color(maskValue / 255f, r, g, b));
                }
            }

            _terrainMaskTexture = ImageTexture.CreateFromImage(image);
            _gridShaderOverlay.UpdateTerrainMask(_terrainMaskTexture, MapWidth, MapHeight);
            _gridShaderOverlay.UpdateOutsideMapColor(OutsideMapColor);
        }

        public void SetShowWalkableOverlay(bool show)
        {
            ShowWalkableOverlay = show;
            QueueRedraw();
        }

        public void SetShowGridCoords(bool show)
        {
            ShowGridCoords = show;
            QueueRedraw();
        }

        private void DrawGridCoords()
        {
            // 绘制每个格子的坐标
            var font = ThemeDB.FallbackFont;
            var fontSize = Mathf.Max(8, GridSize / 6);  // 根据格子大小动态调整字号
            var textColor = new Color(0.8f, 0.8f, 0.8f, 0.7f);  // 浅灰色

            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    var worldPos = GridToWorld(new Vector2I(x, y));
                    var text = $"x:{x}\ny:{y}";

                    // 计算文字位置（居中）
                    var textSize = font.GetMultilineStringSize(text, HorizontalAlignment.Center, -1, fontSize);
                    var textPos = worldPos - new Vector2(textSize.X / 2, textSize.Y / 2);

                    // 绘制文字
                    DrawMultilineString(font, textPos, text, HorizontalAlignment.Center, -1, fontSize, (int)(textSize.Y + 2), textColor);
                }
            }
        }

        // ============ 地图操作 ============

        public Error SaveCurrentMap()
        {
            return MapDataManager.SaveMapToCsv(CurrentMapName, GridData);
        }

        public bool LoadMap(string mapName)
        {
            var loadedData = MapDataManager.LoadMapFromCsv(mapName);
            if (loadedData.Count > 0)
            {
                GridData = loadedData;
                MapHeight = GridData.Count;
                MapWidth = GridData.Count > 0 ? GridData[0].Count : 50;
                CurrentMapName = mapName;
                UpdateGridShaderOverlay();
                UpdateTerrainMask();
                SyncBackgroundSize();
                QueueRedraw();
                return true;
            }
            return false;
        }

        public Error ExportCsv(string exportPath)
        {
            return MapDataManager.ExportCsv(CurrentMapName, exportPath);
        }

        public Error ImportCsv(string importPath)
        {
            var err = MapDataManager.ImportCsv(importPath, CurrentMapName);
            if (err == Error.Ok)
            {
                // 重新加载
                LoadMap(CurrentMapName);
            }
            return err;
        }

    }
}
