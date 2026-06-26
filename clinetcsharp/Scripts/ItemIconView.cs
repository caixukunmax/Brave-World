using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 道具图标视图 —— 可复用的 UI 图标控件，用于背包、提示框、商店等。
    /// </summary>
    [GlobalClass]
    public partial class ItemIconView : Control
    {
        [Export] public bool ShowCount = true;
        [Export] public bool ShowQualityBorder = true;
        [Export] public int QualityBorderWidth = 2;

        private TextureRect _iconRect;
        private Label _countLabel;
        private uint _itemId;

        private uint _pendingItemId;
        private uint _pendingCount;
        private bool _pendingSetup;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(64, 64);
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;

            BuildChildren();

            if (_pendingSetup)
                ApplySetup(_pendingItemId, _pendingCount);
        }

        private void BuildChildren()
        {
            if (_iconRect == null)
            {
                _iconRect = new TextureRect
                {
                    AnchorRight = 1,
                    AnchorBottom = 1,
                    OffsetLeft = 2,
                    OffsetTop = 2,
                    OffsetRight = -2,
                    OffsetBottom = -2,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                };
                AddChild(_iconRect);
            }

            if (_countLabel == null)
            {
                _countLabel = new Label
                {
                    AnchorLeft = 1,
                    AnchorTop = 1,
                    OffsetLeft = -28,
                    OffsetTop = -18,
                    OffsetRight = -2,
                    OffsetBottom = -2,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
                _countLabel.AddThemeFontSizeOverride("font_size", 12);
                _countLabel.AddThemeColorOverride("font_color", Colors.White);
                AddChild(_countLabel);
            }
        }

        public void Setup(uint itemId, uint count)
        {
            if (_iconRect == null)
            {
                _pendingItemId = itemId;
                _pendingCount = count;
                _pendingSetup = true;
                return;
            }

            ApplySetup(itemId, count);
        }

        private void ApplySetup(uint itemId, uint count)
        {
            _itemId = itemId;
            _iconRect.Texture = ItemIconCatalog.GetIcon(itemId);

            if (ShowCount && count > 1)
            {
                _countLabel.Text = $"x{count}";
                _countLabel.Visible = true;
            }
            else
            {
                _countLabel.Visible = false;
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (!ShowQualityBorder) return;

            var quality = TryGetQuality((int)_itemId);
            var color = ItemIconCatalog.GetQualityColor(quality);
            DrawRect(new Rect2(Vector2.Zero, Size), color, false, QualityBorderWidth);
        }

        private static int TryGetQuality(int itemId)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var node = tree?.GetFirstNodeInGroup("inventory_manager");
            if (node is InventoryManager mgr)
                return mgr.GetItemQuality((uint)itemId);
            return 0;
        }
    }
}
