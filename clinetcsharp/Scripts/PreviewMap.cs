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

        /// <summary>无地形格子的底色（必须与 assets/shaders/grid_overlay.gdshader 的 default_fill
        /// 保持一致）。普通地形 color_a=0 时游戏地图显示的就是这个极暗灰（如落叶乡全图），
        /// 预览用它做背景色即可与游戏地图背景一致。</summary>
        public static readonly Color NoTerrainCellFill = new(0.06f, 0.06f, 0.06f, 1.0f);

        /// <summary>预览背景色（A=0 时不绘制，保持旧行为）。ShowGrid=false 时生效，
        /// 用于把预览背景对齐游戏地图底色。</summary>
        [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0);

        /// <summary>预览放大系数（1=按整图自适应）。编辑器想让实体更大可整体放大。</summary>
        [Export] public float PreviewZoom { get; set; } = 1.0f;

        /// <summary>格子像素尺寸。独立于 GridManager 存在，关掉格子后相机/实体定位仍可用。
        /// 外部一旦显式赋值，_Ready 就不再从主 GridManager 回采（见 _gridSizeExplicit）。</summary>
        [Export]
        public int GridSize
        {
            get => _gridSize;
            set { _gridSize = value; _gridSizeExplicit = true; }
        }
        private int _gridSize = 111;
        /// <summary>外部是否显式指定了 GridSize。编辑器插件预览里，主场景 main.tscn 的 GridManager
        /// 节点（序列化值 111，非 [Tool] 在编辑器里不会跑响应式）就在编辑器场景树中，
        /// _Ready 若回采它会覆盖面板从 debug_panel_config.cfg 读到的游戏实际生效值（响应式 136），
        /// 导致铭牌框/血条整体缩水、固定字号文字溢出边框。显式指定后禁止回采。</summary>
        private bool _gridSizeExplicit;

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
            if (mainGrid != null && !_gridSizeExplicit)
            {
                // 仅在没有显式来源时回采主 GridManager；直接写字段，不当作显式覆盖。
                _gridSize = mainGrid.GridSize;
            }
            else if (!_gridSizeExplicit)
            {
                // 默认值 111 没有任何可信来源（既没有主 GridManager，外部也没显式覆盖），
                // 直接报错避免静默回退导致渲染偏差难以排查。
                // 编辑器插件预览等无 GridManager 的场景，必须在构造 PreviewMap 时显式传入 GridSize。
                GD.PushError("[PreviewMap] 无 GridManager 且 GridSize 仍为默认值 111，" +
                    "请在构造时显式设置 GridSize（如从 debug_panel_config.cfg map/grid_size 读取）。");
            }

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
            else if (BackgroundColor.A > 0.0f)
            {
                // ShowGrid=false 时用纯色背景对齐游戏地图底色（无地形格子的 default_fill，
                // 如落叶乡全图）。尺寸给足：任意缩放级别下视口内都不会露出背景之外的部分。
                _background = new ColorRect
                {
                    Name = "Background",
                    Color = BackgroundColor,
                    Position = new Vector2(-50000, -50000),
                    Size = new Vector2(100000, 100000),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                AddChild(_background);
            }

            // 相机（对准地图中心，自动计算 zoom 使地图完整显示）
            _camera = new Camera2D
            {
                Name = "Camera",
                AnchorMode = Camera2D.AnchorModeEnum.DragCenter,
                Enabled = true,
            };
            AddChild(_camera);
            // 在编辑器插件的 SubViewport 中，Camera2D 不会自动 become current，
            // 必须显式 MakeCurrent，否则视口仍从世界 (0,0) 渲染，实体被挤到角落。
            _camera.MakeCurrent();

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
            // 视口尺寸尚未就绪（容器布局未完成）时延迟重试，避免用 0/极小尺寸计算 zoom
            if (viewportSize.X < 2 || viewportSize.Y < 2)
            {
                CallDeferred(nameof(AdjustCamera));
                return;
            }

            // 每次都重新 MakeCurrent：SubViewport 中的 Camera2D 在编辑器 [Tool] 模式下
            // _Ready 阶段的 MakeCurrent 可能不生效（视口尚未开始渲染），
            // 每次调整相机时重新设为 current，确保预览画面始终对准相机视角。
            // MakeCurrent 是幂等的，重复调用无副作用。
            _camera.MakeCurrent();

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
