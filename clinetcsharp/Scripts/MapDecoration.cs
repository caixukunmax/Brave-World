using Render = ClinetCSharp.RenderComponents;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图装饰实体（房舍等静态摆件）。
    /// 作为 entityType="decoration" 的 EntityProfile 运行时表现，通过 ProfileId 驱动外观/标签/障碍属性。
    /// </summary>
    public partial class MapDecoration : EntityBase
    {
        private int _gridSize = 111;
        private int _gridX;
        private int _gridY;

        /// <summary>建筑配置 ID（即旧的 DecorationTypeId / ProfileId）</summary>
        public int BuildCfgId => ProfileId;

        /// <summary>建筑实例唯一 UID</summary>
        public int BuildingUid { get; private set; } = -1;

        public int GridX => _gridX;
        public int GridY => _gridY;

        /// <summary>占地宽度（格子数）</summary>
        public int SizeX => GridSizeX;
        /// <summary>占地高度（格子数）</summary>
        public int SizeY => GridSizeY;

        /// <summary>是否阻塞移动（由 obstacle 组件控制）</summary>
        public bool BlockMovement { get; set; } = false;

        /// <summary>是否为编辑模式下的可拖动摆件</summary>
        public bool IsEditable { get; set; } = false;

        /// <summary>编辑模式下请求开始拖动本摆件</summary>
        public static event System.Action<MapDecoration>? DecorationDragRequested;

        protected override Vector2I GetGridPos() => new Vector2I(_gridX, _gridY);
        protected override int GetGridSize() => _gridSize;
        protected override void SetGridSizeValue(int value) => _gridSize = value;

        public void Setup(int profileId, int gridX, int gridY, int gridSize, int buildingUid = -1, int sizeX = 1, int sizeY = 1)
        {
            ProfileId = profileId;
            BuildingUid = buildingUid;
            _gridX = gridX;
            _gridY = gridY;
            _gridSize = gridSize;
            GridSizeX = sizeX > 0 ? sizeX : 1;
            GridSizeY = sizeY > 0 ? sizeY : 1;

            AddToGroup("map_decoration");
            AddToGroup("decoration");

            Name = $"MapDecoration_{gridX}_{gridY}_{profileId}_{buildingUid}";
            VisualSizeScale = 0.95f; // 稍微留一点格子边距
            CornerRadius = 8f;
            TextColor = new Color(1, 1, 0.9f);

            // 从 EntityProfileManager 应用配置（会设置 GridSizeX/Y 和视觉属性）
            var profileMgr = EntityProfileManager.Instance;
            if (profileMgr != null)
            {
                profileMgr.ApplyProfile(this, profileId);
            }
            else
            {
                GD.PushError($"[MapDecoration] EntityProfileManager 未就绪，无法应用 Profile {profileId}");
            }

            // 应用配置后重新计算占地中心位置（_gridX/_gridY 是左上角锚点）
            Position = GetWorldPositionForGridAnchor(new Vector2I(_gridX, _gridY));

            EnsureRenderComponents();
            QueueRedraw();
        }

        public override void _Input(InputEvent @event)
        {
            if (IsEditable && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                if (HitTest(GetGlobalMousePosition()))
                {
                    DecorationDragRequested?.Invoke(this);
                    GetViewport()?.SetInputAsHandled();
                    return;
                }
            }
            if (CheckEntityClick(@event))
                GetViewport()?.SetInputAsHandled();
        }

        public override bool HitTest(Vector2 worldPos)
        {
            float halfW = GridSize * Mathf.Max(1, GridSizeX) / 2.0f;
            float halfH = GridSize * Mathf.Max(1, GridSizeY) / 2.0f;
            var worldCenter = GetWorldPositionForGridAnchor(new Vector2I(_gridX, _gridY));
            return Mathf.Abs(worldPos.X - worldCenter.X) < halfW &&
                   Mathf.Abs(worldPos.Y - worldCenter.Y) < halfH;
        }

        public override void OnGridSizeChanged()
        {
            Position = GetWorldPositionForGridAnchor(new Vector2I(_gridX, _gridY));
        }

        public override void _Draw()
        {
            foreach (var comp in _renderComponents)
                comp.Draw();
        }

        private void EnsureRenderComponents()
        {
            if (_renderComponents.Count > 0)
                return;

            AddRenderComponent(new Render.AppearanceComponent());
            AddRenderComponent(new Render.LabelComponent());
        }
    }
}
