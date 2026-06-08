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

            // 背景（和主地图一致：黑色）
            _background = new ColorRect
            {
                Name = "Background",
                Color = Colors.Black,
                Size = new Vector2(mapPixelWidth, mapPixelHeight),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AddChild(_background);

            // GridManager（复制主地图渲染参数，使用独立地图名避免加载真实地图）
            _gridManager = new GridManager
            {
                Name = "GridManager",
                CurrentMapName = "__preview_map__",
                GridSize = gridSize,
                MapWidth = PreviewMapWidth,
                MapHeight = PreviewMapHeight,
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
            if (_entity != null && IsInstanceValid(_entity))
            {
                RemoveChild(_entity);
                _entity.QueueFree();
            }

            _entity = entity;
            if (_entity == null) return;

            int centerX = PreviewMapWidth / 2;
            int centerY = PreviewMapHeight / 2;
            _entity.Position = UiUtils.GridToWorld(new Vector2I(centerX, centerY), _gridManager.GridSize);

            // 禁用所有交互和逻辑，仅保留渲染
            _entity.SetProcessInput(false);
            _entity.SetProcess(false);
            _entity.SetPhysicsProcess(false);

            if (_entity is Player player)
                player.SetProcessUnhandledInput(false);

            AddChild(_entity);
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
