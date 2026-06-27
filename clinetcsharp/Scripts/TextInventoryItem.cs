using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    [GlobalClass]
    public partial class TextInventoryItem : Label
    {
        [Export] public string ItemName = "";
        [Export] public uint ItemCount;
        [Export] public int Quality;
        [Export] public uint ItemId;
        [Export] public int FontSize = 16;
        [Export] public Color HoverColor = Colors.Yellow;

        private Color _normalColor;
        private PanelContainer? _dimTooltip;
        private bool _isHovered;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Pass;
            VerticalAlignment = VerticalAlignment.Center;
            AddThemeFontSizeOverride("font_size", FontSize);
            MouseEntered += () =>
            {
                _isHovered = true;
                AddThemeColorOverride("font_color", HoverColor);
                QueueRedraw();
            };
            MouseExited += () =>
            {
                _isHovered = false;
                AddThemeColorOverride("font_color", _normalColor);
                QueueRedraw();
            };
            UpdateVisuals();
        }

        public void Setup(string name, uint count, int quality, uint itemId)
        {
            ItemName = name;
            ItemCount = count;
            Quality = quality;
            ItemId = itemId;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            Text = $"{ItemName}×{ItemCount}";
            _normalColor = ItemIconCatalog.GetQualityColor(Quality);
            AddThemeColorOverride("font_color", _normalColor);
        }

        [Signal]
        public delegate void RightClickedEventHandler(uint itemId, uint count);

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Right)
            {
                EmitSignal(SignalName.RightClicked, ItemId, ItemCount);
                AcceptEvent();
            }
        }

        public override void _Notification(int what)
        {
            base._Notification(what);

            if (what == NotificationMouseEnter)
                ShowDimensionTooltip();
            else if (what == NotificationMouseExit)
                HideDimensionTooltip();
        }

        public override void _Draw()
        {
            base._Draw();

            // 鼠标悬停时绘制一个白色圆角边框，按实际文字大小包裹
            if (_isHovered)
            {
                var font = GetThemeDefaultFont();
                var textSize = font.GetStringSize(Text, fontSize: FontSize);

                int radius = InventoryUI.BackpackHoverCornerRadius;
                int border = InventoryUI.BackpackHoverBoxBorderWidth;
                var color = InventoryUI.BackpackHoverBoxColor;

                // 按实际文字宽度绘制，外扩 border 像素，避免右边出现多余空白
                var boxSize = new Vector2(textSize.X + border * 2, Size.Y + border * 2);
                DrawRoundedRectOutline(new Rect2(new Vector2(-border, -border), boxSize), radius, border, color);
            }
        }

        private void DrawRoundedRectOutline(Rect2 rect, float radius, float width, Color color)
        {
            float r = Mathf.Min(Mathf.Min(radius, rect.Size.X / 2f), rect.Size.Y / 2f);
            const int segmentsPerCorner = 12;
            var points = new List<Vector2>();

            // 顶边（从左到右）
            points.Add(new Vector2(rect.Position.X + r, rect.Position.Y));
            points.Add(new Vector2(rect.Position.X + rect.Size.X - r, rect.Position.Y));

            // 右上角
            Vector2 tr = new Vector2(rect.Position.X + rect.Size.X - r, rect.Position.Y + r);
            for (int i = 1; i <= segmentsPerCorner; i++)
            {
                float angle = -Mathf.Pi / 2f + (Mathf.Pi / 2f) * (i / (float)segmentsPerCorner);
                points.Add(tr + r * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }

            // 右边（从上到下）
            points.Add(new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y + rect.Size.Y - r));

            // 右下角
            Vector2 br = new Vector2(rect.Position.X + rect.Size.X - r, rect.Position.Y + rect.Size.Y - r);
            for (int i = 1; i <= segmentsPerCorner; i++)
            {
                float angle = (Mathf.Pi / 2f) * (i / (float)segmentsPerCorner);
                points.Add(br + r * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }

            // 底边（从右到左）
            points.Add(new Vector2(rect.Position.X + r, rect.Position.Y + rect.Size.Y));

            // 左下角
            Vector2 bl = new Vector2(rect.Position.X + r, rect.Position.Y + rect.Size.Y - r);
            for (int i = 1; i <= segmentsPerCorner; i++)
            {
                float angle = Mathf.Pi / 2f + (Mathf.Pi / 2f) * (i / (float)segmentsPerCorner);
                points.Add(bl + r * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }

            // 左边（从下到上）
            points.Add(new Vector2(rect.Position.X, rect.Position.Y + r));

            // 左上角
            Vector2 tl = new Vector2(rect.Position.X + r, rect.Position.Y + r);
            for (int i = 1; i <= segmentsPerCorner; i++)
            {
                float angle = Mathf.Pi + (Mathf.Pi / 2f) * (i / (float)segmentsPerCorner);
                points.Add(tl + r * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }

            // 闭合
            points.Add(points[0]);

            DrawPolyline(points.ToArray(), color, width, true);
        }

        private void ShowDimensionTooltip()
        {
            if (!InventoryUI.DebugShowItemDimensions)
                return;

            if (_dimTooltip == null)
            {
                _dimTooltip = new PanelContainer
                {
                    MouseFilter = MouseFilterEnum.Ignore,
                    ZIndex = 100,
                };
                var style = new StyleBoxFlat
                {
                    BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                };
                style.SetContentMarginAll(4);
                _dimTooltip.AddThemeStyleboxOverride("panel", style);

                var label = new Label();
                label.AddThemeFontSizeOverride("font_size", 12);
                label.AddThemeColorOverride("font_color", Colors.White);
                _dimTooltip.AddChild(label);
                AddChild(_dimTooltip);
            }

            _dimTooltip.GetChild<Label>(0).Text = $"宽: {Size.X:F0}px\n高: {Size.Y:F0}px";
            _dimTooltip.Visible = true;

            // 放在道具右侧，避免遮挡；用 CallDeferred 确保 PanelContainer 已计算完尺寸
            CallDeferred(nameof(PositionDimensionTooltip));
        }

        private void PositionDimensionTooltip()
        {
            if (_dimTooltip == null) return;
            _dimTooltip.Position = new Vector2(Size.X + 4, 0);
        }

        private void HideDimensionTooltip()
        {
            if (_dimTooltip != null)
                _dimTooltip.Visible = false;
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            SetDragPreview(CreateDragPreview());
            return this;
        }

        private Control CreateDragPreview()
        {
            var label = new Label
            {
                Text = Text,
                Modulate = new Color(1, 1, 1, 0.7f)
            };
            label.AddThemeFontSizeOverride("font_size", FontSize);
            label.AddThemeColorOverride("font_color", ItemIconCatalog.GetQualityColor(Quality));
            return label;
        }
    }
}
