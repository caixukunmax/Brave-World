using Render = ClinetCSharp.RenderComponents;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图装饰实体（房舍等静态摆件）。
    /// 复用 EntityBase 渲染管线：AppearanceComponent 画主体，LabelComponent 画名称。
    /// </summary>
    public partial class MapDecoration : EntityBase
    {
        private int _gridSize = 111;
        private int _gridX;
        private int _gridY;
        private int _decorationTypeId;

        public int DecorationTypeId => _decorationTypeId;
        public int GridX => _gridX;
        public int GridY => _gridY;

        protected override Vector2I GetGridPos() => new Vector2I(_gridX, _gridY);
        protected override int GetGridSize() => _gridSize;
        protected override void SetGridSizeValue(int value) => _gridSize = value;

        public void Setup(int decorationTypeId, int gridX, int gridY, int gridSize)
        {
            _decorationTypeId = decorationTypeId;
            _gridX = gridX;
            _gridY = gridY;
            _gridSize = gridSize;

            AddToGroup("map_decoration");

            var cfg = DecorationConfigUtil.Get(decorationTypeId);
            Name = $"MapDecoration_{gridX}_{gridY}_{cfg.Name}";
            BgColor = cfg.Color;
            BorderColor = cfg.BorderColor;
            BgOpacity = cfg.Color.A;
            TextColor = new Color(1, 1, 0.9f);
            VisualSizeScale = 0.95f; // 稍微留一点格子边距
            CornerRadius = 8f;

            // 标签：第一行显示装饰名称
            SetLabelText(0, cfg.DisplayName);
            LabelCenterX[0] = true;
            LabelYOffsets[0] = 0;

            Position = UiUtils.GridToWorld(gridX, gridY, gridSize);

            EnsureRenderComponents();
            QueueRedraw();
        }

        public override void _Input(InputEvent @event)
        {
            CheckEntityClick(@event);
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
