using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 预览地图 — 完全等同于一张真实地图的迷你版本。
    /// 用于模板预览面板，确保预览效果与主地图 1:1 一致。
    /// 特性：无输入、无 AI、无网络同步，仅用于视觉预览。
    /// </summary>
    // [Tool]：编辑器插件「实体配置」面板也内嵌本类做预览。不加 [Tool] 时编辑器下 _Ready 不执行，
    // 相机永远不创建，AutoFit/缩放滑条全部静默失效。_Ready 路径全部判空/纯节点操作，
    // 编辑器里 ShowGrid=false 会跳过 GridManager/地图数据分支，无运行时副作用。
    [Tool]
    public partial class PreviewMap : Node2D
    {
        [Export] public int PreviewMapWidth { get; set; } = 5;
        [Export] public int PreviewMapHeight { get; set; } = 5;

        /// <summary>是否渲染背景格子。编辑器预览只想要实体本身时可关掉。</summary>
        [Export] public bool ShowGrid { get; set; } = true;

        /// <summary>预览放大系数（1=按整图自适应）。编辑器想让实体更大可整体放大。</summary>
        [Export] public float PreviewZoom { get; set; } = 1.0f;

        /// <summary>格子像素尺寸。独立于 GridManager 存在，关掉格子后相机/实体定位仍可用。</summary>
        [Export] public int GridSize { get; set; } = 111;

        private GridManager _gridManager;
        private ColorRect _background;
        private Camera2D _camera;
        private EntityBase _entity;

        [Export] public float MinZoom { get; set; } = 0.1f;
        [Export] public float MaxZoom { get; set; } = 20.0f;
        [Export] public float ZoomStep { get; set; } = 0.2f;

        /// <summary>用户缩放倍率（在 AutoFit 自适应基础上叠加）。编辑器用滑条控制，运行时用滚轮控制。</summary>
        [Export] public float UserZoom { get; set; } = 1.0f;
        [Export] public float MinUserZoom { get; set; } = 0.2f;
        [Export] public float MaxUserZoom { get; set; } = 5.0f;
        /// <summary>是否允许鼠标滚轮缩放。编辑器已改为滑条控制，故设为 false；运行时预览面板保持 true。</summary>
        [Export] public bool EnableWheelZoom { get; set; } = true;
        /// <summary>UserZoom 变化通知（供外部滑条同步显示）。</summary>
        public event System.Action<float> UserZoomChanged;

        public override void _Ready()
        {
            var mainGrid = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
            GridSize = mainGrid?.GridSize ?? 111;

            int mapPixelWidth = PreviewMapWidth * GridSize;
            int mapPixelHeight = PreviewMapHeight * GridSize;

            if (ShowGrid)
            {
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
                    GridSize = GridSize,
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
            }

            // 相机（对准地图中心，自动计算 zoom 使地图完整显示）
            _camera = new Camera2D
            {
                Name = "Camera",
                AnchorMode = Camera2D.AnchorModeEnum.DragCenter,
            };
            AddChild(_camera);

            CallDeferred(nameof(AdjustCamera));
        }

        /// <summary>
        /// 根据实体占地自动缩放，使实体约占视口较短边的 50% 并居中。
        /// 编辑器预览调用：无论 1x1 / 2x2 / 异形占地，都保持适中大小显示且默认居中。
        /// </summary>
        public void AutoFit(EntityBase entity)
        {
            if (entity == null) return;

            int gs = GridSize;
            int footprint = Mathf.Max(1, Mathf.Max(entity.GridSizeX, entity.GridSizeY));
            float entityWorldSize = gs * footprint;

            var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(640, 640);
            float target = 0.5f * Mathf.Min(vp.X, vp.Y);

            // AdjustCamera 内部已算 baseFit = min(vp/地图) * 0.9，最终 zoom = baseFit * PreviewZoom。
            // 反推 PreviewZoom，使 entityWorldSize * zoom ≈ target。
            float baseFit = Mathf.Min(vp.X / (PreviewMapWidth * gs), vp.Y / (PreviewMapHeight * gs)) * 0.9f;
            if (baseFit <= 0.0001f) baseFit = 1f;

            float desired = target / (entityWorldSize * baseFit);
            PreviewZoom = Mathf.Clamp(desired, 0.3f, 8.0f);
            AdjustCamera();
        }

        public void AdjustCamera()
        {
            if (_camera == null || GetViewport() == null) return;

            var viewportSize = GetViewport().GetVisibleRect().Size;
            int mapPixelWidth = PreviewMapWidth * GridSize;
            int mapPixelHeight = PreviewMapHeight * GridSize;

            // 对准地图中心
            _camera.Position = new Vector2(mapPixelWidth / 2f, mapPixelHeight / 2f);

            // 计算 zoom 使地图完整显示在视口中（留 10% 边距）
            float zoomX = viewportSize.X / mapPixelWidth;
            float zoomY = viewportSize.Y / mapPixelHeight;
            float zoom = Mathf.Min(zoomX, zoomY) * 0.9f * PreviewZoom * UserZoom;
            zoom = Mathf.Clamp(zoom, 0.1f, 20.0f);

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
            entity.SetGridSize(GridSize);

            int mapPixelWidth = PreviewMapWidth * GridSize;
            int mapPixelHeight = PreviewMapHeight * GridSize;

            // 居中：直接把实体的渲染中心（含 2x2 / 异形多格占地）放到地图正中心。
            // 不要用 GetWorldPositionForGridPos(中心格)，锚点取整会让多格实体偏到右下角。
            // Player 与 MapDecoration 的绘制都以 Position 为渲染中心，故两者都直接居中。
            entity.Position = new Vector2(mapPixelWidth / 2.0f, mapPixelHeight / 2.0f);

            // 禁用所有交互和逻辑，仅保留渲染
            entity.SetProcessInput(false);
            entity.SetProcessUnhandledInput(false);
            entity.SetProcess(false);
            entity.SetPhysicsProcess(false);

            if (entity is Player player)
                player.SetProcessUnhandledInput(false);
        }

        /// <summary>以地图中心为锚点进行缩放（供外部面板/滚轮转发；direction&lt;0 放大）。
        /// 统一改为调整 UserZoom（在 AutoFit 自适应基础上叠加），不直接改相机 Zoom，
        /// 这样编辑器滑条与运行时滚轮走同一套逻辑，且编辑占地(触发 AutoFit)后用户缩放不丢失。</summary>
        public void ApplyZoom(int direction)
        {
            float factor = 1f + ZoomStep;
            float z = direction < 0 ? UserZoom * factor : UserZoom / factor;
            SetUserZoom(z);
        }

        /// <summary>设置用户缩放倍率（MinUserZoom~MaxUserZoom），在 AutoFit 自适应基础上叠加。
        /// 编辑器滑条通过它控制预览缩放。</summary>
        public void SetUserZoom(float z)
        {
            UserZoom = Mathf.Clamp(z, MinUserZoom, MaxUserZoom);
            AdjustCamera();
            UserZoomChanged?.Invoke(UserZoom);
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
            if (!EnableWheelZoom) return;
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    ApplyZoom(-1);
                    GetViewport()?.SetInputAsHandled();
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    ApplyZoom(1);
                    GetViewport()?.SetInputAsHandled();
                }
            }
        }
    }
}
