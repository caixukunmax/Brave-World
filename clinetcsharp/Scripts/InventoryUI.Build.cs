using Godot;

namespace ClinetCSharp
{
    public partial class InventoryUI
    {
        private Label _capacityLabel;
        private TextInventoryContainer _inventoryContainer;

        public const int InventoryFontSize = 16;
        public const int InventoryLineSpacing = 8;

        private void BuildContent()
        {
            // 容量标签：放在背包内容下方，右对齐
            _capacityLabel = new Label
            {
                Text = "0 / 300",
                HorizontalAlignment = HorizontalAlignment.Right,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _capacityLabel.AddThemeFontSizeOverride("font_size", 12);
            _capacityLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));

            // 根据目标总宽度计算每行字符数
            int lineWidth = Mathf.Max(1, BackpackContentWidth / InventoryFontSize);
            float contentWidth = lineWidth * InventoryFontSize;
            BackpackContentWidth = (int)contentWidth;

            // 根据目标总高度计算行数，但不能超过当前面板可容纳的高度
            int lineHeight = InventoryFontSize + InventoryLineSpacing;
            float maxAreaHeight = GetMaxBackpackAreaHeight();
            int clampedHeight = Mathf.Min(BackpackAreaHeight, Mathf.FloorToInt(maxAreaHeight));
            int lineCount = Mathf.Max(1, Mathf.RoundToInt(clampedHeight / (float)lineHeight));
            float containerHeight = lineCount * lineHeight;

            // 同步静态值，让 DebugPanel 下次显示的是实际生效高度
            BackpackAreaHeight = (int)containerHeight;

            _inventoryContainer = new TextInventoryContainer
            {
                LineWidth = lineWidth,
                LineCount = lineCount,
                FontSize = InventoryFontSize,
                ItemSpacing = BackpackItemSpacing,
                DrawDebugBorder = DebugDrawItemBorder,
                CustomMinimumSize = new Vector2(contentWidth, containerHeight),
            };
            _inventoryContainer.ItemRightClicked += OnItemRightClicked;

            // 背包区左右/上方留边距，确保不压到面板边框
            var marginContainer = new MarginContainer
            {
                CustomMinimumSize = new Vector2(
                    contentWidth + BackpackPaddingH * 2,
                    containerHeight),
            };
            marginContainer.AddThemeConstantOverride("margin_left", BackpackPaddingH);
            marginContainer.AddThemeConstantOverride("margin_right", BackpackPaddingH);
            marginContainer.AddThemeConstantOverride("margin_top", BackpackPaddingTop);
            marginContainer.AddThemeConstantOverride("margin_bottom", 0);
            marginContainer.AddChild(_inventoryContainer);

            // 弹性占位，让容量标签始终贴在内容区底部（面板高度不变时）
            var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };

            _content.AddChild(marginContainer);
            _content.AddChild(spacer);
            _content.AddChild(_capacityLabel);
        }

        /// <summary>
        /// 当前面板高度下，背包显示区可用的最大高度（留出标题栏、容量标签和容器间距）。
        /// </summary>
        private float GetMaxBackpackAreaHeight()
        {
            var titleBar = GetNodeOrNull<Control>("VBoxContainer/TitleBar");
            float titleBarHeight = titleBar != null && titleBar.Size.Y > 0 ? titleBar.Size.Y : 32f;

            var panelStyle = GetThemeStylebox("panel");
            float panelMarginV = (panelStyle?.ContentMarginTop ?? 0f) + (panelStyle?.ContentMarginBottom ?? 0f);

            float vboxSeparation = GetNodeOrNull<VBoxContainer>("VBoxContainer")?.GetThemeConstant("separation") ?? 4f;
            float contentSeparation = _content.GetThemeConstant("separation");
            const float capacityLabelHeight = 16f;

            float contentHeight = Size.Y - panelMarginV - titleBarHeight - vboxSeparation;
            return Mathf.Max(InventoryFontSize + InventoryLineSpacing,
                contentHeight - contentSeparation - capacityLabelHeight);
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
