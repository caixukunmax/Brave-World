using Godot;

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

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Pass;
            VerticalAlignment = VerticalAlignment.Center;
            AddThemeFontSizeOverride("font_size", FontSize);
            MouseEntered += () => AddThemeColorOverride("font_color", HoverColor);
            MouseExited += () => AddThemeColorOverride("font_color", _normalColor);
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
