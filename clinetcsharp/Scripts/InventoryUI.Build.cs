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
            _content.AddChild(_grid);
        }

        private Button BuildItemButton(InventoryManager.ItemSlot slot)
        {
            var button = new Button
            {
                CustomMinimumSize = new Vector2(70, 70),
                Text = $"{slot.Name}\nX{slot.Count}",
                ClipText = true,
            };

            button.AddThemeColorOverride("font_color", Colors.White);
            button.AddThemeColorOverride("font_hover_color", Colors.Yellow);

            uint itemId = slot.ItemId;
            uint count = slot.Count;
            button.Pressed += () => OnItemClicked(itemId, count);
            return button;
        }

        private Button BuildEmptySlotButton()
        {
            var button = new Button
            {
                CustomMinimumSize = new Vector2(70, 70),
                Disabled = true,
            };
            button.AddThemeColorOverride("font_disabled_color", new Color(0.5f, 0.5f, 0.5f, 0.3f));
            return button;
        }
    }
}
