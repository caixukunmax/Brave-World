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
        [Export] public int GridSize { get; set; } = 64;
        [Export] public int MapWidth { get; set; } = 50;
        [Export] public int MapHeight { get; set; } = 50;
        [Export] public Color LineColor { get; set; } = new Color(0.7f, 0.7f, 0.7f);
        [Export] public float LineWidth { get; set; } = 1.0f;
        [Export] public float DashLength { get; set; } = 8.0f;
        [Export] public float GapLength { get; set; } = 4.0f;

        // 线宽自适应设置
        [Export] public bool AutoLineWidth { get; set; } = true;  // 是否启用自动线宽
        [Export] public float LineWidthScale { get; set; } = 2.0f;  // 线宽比例系数（可调节）
        [Export] public float MinScreenLineWidth { get; set; } = 0.1f;  // 最小屏幕线宽（像素）- 允许细线
        [Export] public float MaxScreenLineWidth { get; set; } = 5.0f;  // 最大屏幕线宽（像素）

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

        // 被移除格子的显示设置
        [Export] public Color RemovedCellColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.5f);  // 默认半透明灰色
        [Export] public bool ShowRemovedCells { get; set; } = true;  // 是否显示被移除的格子

        // 自适应校准用的 zoom 跟踪
        private float _lastCameraZoom = 0.0f;

        public override void _Ready()
        {
            LoadMapData();

            // 启用 2D 像素对齐：防止子像素偏移导致的闪烁
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
                GetTree().Root.SizeChanged += OnViewportResized;
            }

            // 初始化 zoom 跟踪
            _lastCameraZoom = GetCameraZoom();

            QueueRedraw();
        }

        public override void _Process(double _delta)
        {
            // 自适应校准模式下，检测相机 zoom 变化并重新绘制
            if (AdaptiveCalibrationEnabled)
            {
                var currentZoom = GetCameraZoom();
                if (Mathf.Abs(currentZoom - _lastCameraZoom) > 0.001f)
                {
                    _lastCameraZoom = currentZoom;
                    QueueRedraw();
                }
            }
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
                    cell.Exists = true;
                    cell.Walkable = true;
                    cell.Visible = true;
                    row.Add(cell);
                }
                GridData.Add(row);
            }
        }

        public override void _Draw()
        {
            DrawGrid();
            if (IsEditMode && ShowWalkableOverlay)
                DrawWalkableOverlay();
            if (ShowGridCoords)
                DrawGridCoords();
        }

        private void DrawGrid()
        {
            // 获取相机 zoom
            var cameraZoom = GetCameraZoom();

            // 防止 zoom 过小导致线宽计算异常
            cameraZoom = Mathf.Max(cameraZoom, 0.01f);

            // 计算屏幕线宽（优先级：预览值 > 自适应校准 > 手动模式）
            float screenLineWidth;
            if (_previewLineWidth > 0)
            {
                // 预览模式：使用预览值（滑块拖动中）
                screenLineWidth = _previewLineWidth;
            }
            else if (AdaptiveCalibrationEnabled)
            {
                // 自适应校准模式：根据相机zoom实时计算
                screenLineWidth = GetAdaptiveLineWidth(cameraZoom);
                // 限制在最小/最大范围内
                screenLineWidth = Mathf.Clamp(screenLineWidth, MinScreenLineWidth, MaxScreenLineWidth);
            }
            else
            {
                // 手动模式：使用设置的线宽，但根据 zoom 自动调整以确保可见
                // 关键公式：屏幕线宽 = 基础线宽，但要保证最小值
                screenLineWidth = Mathf.Max(LineWidthScale, MinScreenLineWidth);
            }

            // 确保屏幕线宽不会太小（防止在远处看不见）
            screenLineWidth = Mathf.Max(screenLineWidth, MinScreenLineWidth);

            // 确保最小 0.5px，防止 round 后变成 0
            screenLineWidth = Mathf.Max(screenLineWidth, 0.5f);

            // 转换到世界坐标：世界线宽 = 屏幕线宽 / zoom
            // 例如：屏幕要显示 1.5px 线宽，当 zoom=0.4 时，世界线宽 = 1.5 / 0.4 = 3.75
            var lineWidthWorld = screenLineWidth / cameraZoom;

            // 限制世界线宽在合理范围（防止极端值）
            // 最小 0.01（允许细线），最大不超过格子大小的 30%（防止太粗）
            lineWidthWorld = Mathf.Clamp(lineWidthWorld, 0.01f, GridSize * 0.3f);

            // 绘制所有格子的边框
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    var cell = GridData[y][x];
                    if (cell.Exists)
                        DrawCellBorderNormal(x, y, lineWidthWorld);
                    else
                    {
                        // 被移除的格子
                        if (ShowRemovedCells)
                        {
                            var pos = new Vector2(x * GridSize, y * GridSize);
                            var rect = new Rect2(pos, new Vector2(GridSize, GridSize));
                            DrawRect(rect, RemovedCellColor, true);
                        }
                        DrawCellBorderRemoved(x, y, lineWidthWorld);
                    }
                }
            }
        }

        private void DrawCellBorderNormal(int x, int y, float lineWidth)
        {
            // 绘制存在的格子的完整边框（使用填充矩形，避免 draw_rect 边框的闪烁问题）
            var pos = new Vector2(x * GridSize, y * GridSize);
            var size = new Vector2(GridSize, GridSize);

            // 使用填充矩形绘制四条边，抗锯齿效果更好
            // 上边
            DrawRect(new Rect2(pos, new Vector2(size.X, lineWidth)), LineColor, true);
            // 下边
            DrawRect(new Rect2(pos + new Vector2(0, size.Y - lineWidth), new Vector2(size.X, lineWidth)), LineColor, true);
            // 左边
            DrawRect(new Rect2(pos, new Vector2(lineWidth, size.Y)), LineColor, true);
            // 右边
            DrawRect(new Rect2(pos + new Vector2(size.X - lineWidth, 0), new Vector2(lineWidth, size.Y)), LineColor, true);
        }

        private void DrawCellBorderRemoved(int x, int y, float lineWidth)
        {
            // 绘制被移除格子的智能边框
            // 只在与存在的格子相邻的方向显示边（作为存在的格子的边界）
            // 使用填充矩形避免闪烁
            var pos = new Vector2(x * GridSize, y * GridSize);
            var size = new Vector2(GridSize, GridSize);

            // 检查4个方向的邻居是否存在
            bool hasLeft = x > 0 && GridData[y][x - 1].Exists;
            bool hasRight = x < MapWidth - 1 && GridData[y][x + 1].Exists;
            bool hasTop = y > 0 && GridData[y - 1][x].Exists;
            bool hasBottom = y < MapHeight - 1 && GridData[y + 1][x].Exists;

            // 只绘制与存在的格子相邻的边（使用填充矩形）
            // 上边
            if (hasTop)
                DrawRect(new Rect2(pos, new Vector2(size.X, lineWidth)), LineColor, true);
            // 下边
            if (hasBottom)
                DrawRect(new Rect2(pos + new Vector2(0, size.Y - lineWidth), new Vector2(size.X, lineWidth)), LineColor, true);
            // 左边
            if (hasLeft)
                DrawRect(new Rect2(pos, new Vector2(lineWidth, size.Y)), LineColor, true);
            // 右边
            if (hasRight)
                DrawRect(new Rect2(pos + new Vector2(size.X - lineWidth, 0), new Vector2(lineWidth, size.Y)), LineColor, true);
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

        private void DrawWalkableOverlay()
        {
            // 编辑模式下显示可行走/不可行走区域
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    var cell = GridData[y][x];
                    if (!cell.Exists)
                        continue;  // 不存在的格子跳过

                    var pos = new Vector2(x * GridSize, y * GridSize);
                    var rect = new Rect2(pos, new Vector2(GridSize, GridSize));

                    if (!cell.Walkable)
                    {
                        // 不可行走显示红色半透明
                        DrawRect(rect, new Color(1, 0, 0, 0.3f), true);
                    }
                    else if (!cell.Visible)
                    {
                        // 不可见显示灰色半透明
                        DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f), true);
                    }
                    else
                    {
                        // 根据地形类型显示不同颜色
                        var terrainColor = GetTerrainColor(cell.TerrainType);
                        if (terrainColor != Colors.Transparent)
                            DrawRect(rect, terrainColor, true);
                    }
                }
            }
        }

        private Color GetTerrainColor(int terrainType)
        {
            return terrainType switch
            {
                1 => new Color(0.2f, 0.4f, 0.8f, 0.2f),  // 水
                2 => new Color(0.2f, 0.8f, 0.2f, 0.2f),  // 草地
                3 => new Color(0.9f, 0.8f, 0.4f, 0.2f),  // 沙地
                4 => new Color(0.5f, 0.5f, 0.5f, 0.3f),  // 岩石
                _ => Colors.Transparent
            };
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
                return false;
            if (_blockedByChest.Contains(gridPos))
                return false;
            var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (mm != null && mm.IsBlockedByMonster(gridPos))
                return false;
            var nm = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            if (nm != null && nm.IsBlockedByNpc(gridPos))
                return false;
            var cell = GridData[gridPos.Y][gridPos.X];
            return cell.Exists && cell.Walkable;
        }

        public bool IsCellVisible(Vector2I gridPos)
        {
            if (!IsInBounds(gridPos))
                return false;
            var cell = GridData[gridPos.Y][gridPos.X];
            return cell.Exists && cell.Visible;
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

            // 防止 zoom 过小
            zoom = Mathf.Max(zoom, 0.01f);

            // 防除零：如果两个zoom值相同，退化为简单反比缩放
            if (Mathf.Abs(RefZoomB - RefZoomA) < EPSILON)
            {
                var avgZoom = (RefZoomA + RefZoomB) / 2.0f;
                var avgWidth = (RefWidthA + RefWidthB) / 2.0f;
                return avgWidth * avgZoom / zoom;
            }

            // 动态排序：确定哪个是低zoom、哪个是高zoom
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

            // 范围外：反比外推（确保视野拉远时线宽增加）
            if (zoom < lowZoom)
                return lowWidth * lowZoom / zoom;
            else if (zoom > highZoom)
                return highWidth * highZoom / zoom;

            // 范围内：线性插值
            var t = (zoom - lowZoom) / (highZoom - lowZoom);
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
        }

        public void SetLineBrightness(float brightness)
        {
            LineColor = new Color(brightness, brightness, brightness, 1.0f);
            QueueRedraw();
        }

        public void SetEditMode(bool enabled)
        {
            IsEditMode = enabled;
            ShowWalkableOverlay = enabled;
            QueueRedraw();
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
                    var cell = GridData[y][x];
                    if (!cell.Exists)
                        continue;  // 不存在的格子跳过

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
