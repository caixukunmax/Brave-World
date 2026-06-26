using Godot;

namespace ClinetCSharp
{
    public partial class InventoryUI
    {
        private void BuildContent()
        {
            _content.AddChild(new HSeparator());

            _grid = new GridContainer
            {
                Columns = 5,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            _grid.AddThemeConstantOverride("h_separation", 8);
            _grid.AddThemeConstantOverride("v_separation", 8);
            _content.AddChild(_grid);
        }

        private Button BuildItemButton(InventoryManager.ItemSlot slot, InventoryManager manager)
        {
            var button = new Button
            {
                CustomMinimumSize = new Vector2(76, 76),
                Text = "",
                ClipText = true,
            };

            button.AddThemeColorOverride("font_color", Colors.White);
            button.AddThemeColorOverride("font_hover_color", Colors.Yellow);

            int quality = manager.GetItemQuality(slot.ItemId);
            var qualityColor = ItemIconCatalog.GetQualityColor(quality);

            // 品质色边框 + 暗色背景
            var bgPanel = CreateSlotBackground(
                new Color(0.08f, 0.08f, 0.08f, 0.85f),
                qualityColor,
                2);
            button.AddChild(bgPanel);

            // 图标
            var iconView = new ItemIconView
            {
                AnchorLeft = 0,
                AnchorTop = 0,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetLeft = 6,
                OffsetTop = 14,
                OffsetRight = -6,
                OffsetBottom = -6,
                MouseFilter = MouseFilterEnum.Ignore,
                ShowQualityBorder = false,
            };
            button.AddChild(iconView);
            iconView.Setup(slot.ItemId, slot.Count);

            // 道具名（左上角）
            var nameLabel = new Label
            {
                AnchorLeft = 0,
                AnchorTop = 0,
                OffsetLeft = 5,
                OffsetTop = 4,
                Text = slot.Name,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 9);
            nameLabel.AddThemeColorOverride("font_color", Colors.White);
            nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            nameLabel.AddThemeConstantOverride("outline_size", 2);
            button.AddChild(nameLabel);

            uint itemId = slot.ItemId;
            uint count = slot.Count;
            button.Pressed += () => OnItemClicked(itemId, count);
            return button;
        }

        private Button BuildEmptySlotButton()
        {
            var button = new Button
            {
                CustomMinimumSize = new Vector2(76, 76),
                Disabled = true,
            };
            button.AddThemeColorOverride("font_disabled_color", new Color(0.5f, 0.5f, 0.5f, 0.3f));

            var bgPanel = CreateSlotBackground(
                new Color(0.08f, 0.08f, 0.08f, 0.4f),
                new Color(0.3f, 0.3f, 0.3f, 0.5f),
                1);
            button.AddChild(bgPanel);

            return button;
        }

        private static Panel CreateSlotBackground(Color bgColor, Color borderColor, int borderWidth)
        {
            var panel = new Panel
            {
                AnchorLeft = 0,
                AnchorTop = 0,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetLeft = 2,
                OffsetTop = 2,
                OffsetRight = -2,
                OffsetBottom = -2,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            var styleBox = new StyleBoxFlat
            {
                BgColor = bgColor,
                BorderWidthBottom = borderWidth,
                BorderWidthLeft = borderWidth,
                BorderWidthRight = borderWidth,
                BorderWidthTop = borderWidth,
                BorderColor = borderColor,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
            };
            panel.AddThemeStyleboxOverride("panel", styleBox);
            return panel;
        }
    }
}
