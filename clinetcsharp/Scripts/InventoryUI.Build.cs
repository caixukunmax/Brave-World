using Godot;

namespace ClinetCSharp
{
    public partial class InventoryUI
    {
        private Label _capacityLabel;
        private TextInventoryContainer _inventoryContainer;

        private void BuildContent()
        {
            _content.AddChild(new HSeparator());

            _capacityLabel = new Label
            {
                Text = "0 / 300",
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            _capacityLabel.AddThemeFontSizeOverride("font_size", 12);
            _capacityLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            _content.AddChild(_capacityLabel);

            _inventoryContainer = new TextInventoryContainer
            {
                LineWidth = 30,
                LineCount = 10,
                FontSize = 16,
                CustomMinimumSize = new Vector2(480, 260),
            };
            _inventoryContainer.ItemRightClicked += OnItemRightClicked;
            _content.AddChild(_inventoryContainer);
        }

        private void UpdateCapacityLabel()
        {
            var (used, total) = _inventoryContainer.GetCapacity();
            _capacityLabel.Text = $"{used} / {total}";

            if (used >= total)
                _capacityLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
            else if (used >= total * 0.8f)
                _capacityLabel.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.2f));
            else
                _capacityLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        }
    }
}
