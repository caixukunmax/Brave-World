using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 预览地图 — 完全等同于一张真实地图的迷你版本。
    /// 用于模板预览面板，确保预览效果与主地图 1:1 一致。
    /// 特性：无输入、无 AI、无网络同步，仅用于视觉预览。
    /// </summary>
    public partial class PreviewMap : Node2D
    {
        [Export] public int PreviewMapWidth { get; set; } = 5;
        [Export] public int PreviewMapHeight { get; set; } = 5;

        private GridManager _gridManager;
        private ColorRect _background;
        private Camera2D _camera;
        private EntityBase _entity;

        [Export] public float MinZoom { get; set; } = 0.1f;
        [Export] public float MaxZoom { get; set; } = 5.0f;
        [Export] public float ZoomStep { get; set; } = 0.15f;

        public override void _Ready()
        {
            var mainGrid = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
            int gridSize = mainGrid?.GridSize ?? 111;

            int mapPixelWidth = PreviewMapWidth * gridSize;
            int mapPixelHeight = PreviewMapHeight * gridSize;

            // 背景（透明，由 shader 为实际格子绘制黑色底）
            _background = new ColorRect
            {
                Name = "Background",
                Color = Colors.Transparent,
                Size = new Vector2(mapPixelWidth, mapPixelHeight),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AddChild(_background);

            // GridManager（复制主地图渲染参数，使用独立数据避免加载真实地图）
            _gridManager = new GridManager
            {
                Name = "GridManager",
                SkipAutoLoad = true,
                GridSize = gridSize,
                LineColor = mainGrid?.LineColor ?? new Color(0.7f, 0.7f, 0.7f),
                LineWidth = mainGrid?.LineWidth ?? 1.0f,
                DashLength = mainGrid?.DashLength ?? 8.0f,
                GapLength = mainGrid?.GapLength ?? 4.0f,
                AutoLineWidth = mainGrid?.AutoLineWidth ?? true,
                LineWidthScale = mainGrid?.LineWidthScale ?? 1.0f,
                MinScreenLineWidth = mainGrid?.MinScreenLineWidth ?? 1.0f,
                MaxScreenLineWidth = mainGrid?.MaxScreenLineWidth ?? 2.0f,
                GridAntiAliasSoftness = mainGrid?.GridAntiAliasSoftness ?? GridOverlayAntiAliasSoftnessPolicy.Default,
                ShowGridCoords = false,
                ShowTerrainLabels = false,
                IsEditMode = false,
            };
            AddChild(_gridManager);

            // 手动填充预览地图数据（矩形），并刷新渲染
            _gridManager.GridData = MapDataManager.CreateDefaultGridData(PreviewMapWidth, PreviewMapHeight);
            _gridManager.RecalculateMapBounds();
            _gridManager.UpdateGridShaderOverlay();
            _gridManager.NotifyTerrainChanged();
            _gridManager.SyncBackgroundSize();

            // 相机（对准地图中心，自动计算 zoom 使地图完整显示）
            _camera = new Camera2D
            {
                Name = "Camera",
                AnchorMode = Camera2D.AnchorModeEnum.DragCenter,
            };
            AddChild(_camera);

            CallDeferred(nameof(AdjustCamera));
        }

        private void AdjustCamera()
        {
            if (_camera == null || GetViewport() == null) return;

            var viewportSize = GetViewport().GetVisibleRect().Size;
            int mapPixelWidth = PreviewMapWidth * _gridManager.GridSize;
            int mapPixelHeight = PreviewMapHeight * _gridManager.GridSize;

            // 对准地图中心
            _camera.Position = new Vector2(mapPixelWidth / 2f, mapPixelHeight / 2f);

            // 计算 zoom 使地图完整显示在视口中（留 10% 边距）
            float zoomX = viewportSize.X / mapPixelWidth;
            float zoomY = viewportSize.Y / mapPixelHeight;
            float zoom = Mathf.Min(zoomX, zoomY) * 0.9f;
            zoom = Mathf.Clamp(zoom, 0.1f, 5.0f);

            _camera.Zoom = new Vector2(zoom, zoom);
        }

        /// <summary>在地图中心放置实体，并禁用其输入/AI/物理</summary>
        public void SetEntity(EntityBase entity)
        {
            // 如果传入的是当前已挂载的同一个实体，直接刷新状态即可。
            // 避免 RemoveChild + QueueFree + AddChild 导致同一实例被释放而无法渲染。
            if (_entity != null && IsInstanceValid(_entity) && _entity == entity)
            {
                ApplyEntityPreviewState(_entity);
                return;
            }

            if (_entity != null && IsInstanceValid(_entity))
            {
                RemoveChild(_entity);
                _entity.QueueFree();
            }

            _entity = entity;
            if (_entity == null) return;

            ApplyEntityPreviewState(_entity);
            AddChild(_entity);
        }

        /// <summary>将实体调整为预览状态：同步 GridSize、居中、禁用交互逻辑</summary>
        private void ApplyEntityPreviewState(EntityBase entity)
        {
            entity.SetGridSize(_gridManager.GridSize);

            int centerX = PreviewMapWidth / 2;
            int centerY = PreviewMapHeight / 2;

            // 占地型建筑（MapDecoration）的 Position 是左上角锚点的渲染中心，
            // 不能直接用格子中心，否则会根据占地大小偏移到错误位置。
            if (entity is MapDecoration decoration)
                decoration.Position = decoration.GetWorldPositionForGridPos(new Vector2I(centerX, centerY));
            else
                entity.Position = UiUtils.GridToWorld(new Vector2I(centerX, centerY), _gridManager.GridSize);

            // 禁用所有交互和逻辑，仅保留渲染
            entity.SetProcessInput(false);
            entity.SetProcessUnhandledInput(false);
            entity.SetProcess(false);
            entity.SetPhysicsProcess(false);

            if (entity is Player player)
                player.SetProcessUnhandledInput(false);
        }

        /// <summary>以地图中心为锚点进行缩放（供外部面板转发滚轮事件）</summary>
        public void ApplyZoom(int direction)
        {
            if (_camera == null) return;

            float zoomFactor = 1f + ZoomStep;
            float newZoom = direction < 0
                ? _camera.Zoom.X * zoomFactor
                : _camera.Zoom.X / zoomFactor;

            newZoom = Mathf.Clamp(newZoom, MinZoom, MaxZoom);
            _camera.Zoom = new Vector2(newZoom, newZoom);
        }

        /// <summary>清除当前实体</summary>
        public void ClearEntity()
        {
            if (_entity != null && IsInstanceValid(_entity))
            {
                RemoveChild(_entity);
                _entity.QueueFree();
            }
            _entity = null;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    ApplyZoom(-1, mouseButton.Position);
                    GetViewport().SetInputAsHandled();
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    ApplyZoom(1, mouseButton.Position);
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private void ApplyZoom(int direction, Vector2 mousePosition)
        {
            if (_camera == null) return;

            float zoomFactor = 1f + ZoomStep;
            float newZoom = direction < 0
                ? _camera.Zoom.X * zoomFactor
                : _camera.Zoom.X / zoomFactor;

            newZoom = Mathf.Clamp(newZoom, MinZoom, MaxZoom);
            _camera.Zoom = new Vector2(newZoom, newZoom);
        }
    }
}
