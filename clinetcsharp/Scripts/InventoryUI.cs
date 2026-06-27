using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// Inventory panel shell and lifecycle.
    /// Text-based inventory construction and interactions live in partial files.
    /// </summary>
    public partial class InventoryUI : DraggablePanel
    {
        private VBoxContainer _content;
        private bool _initialPlacementPending;

        // 运行时可在 DebugPanel-系统-背包 中调整的静态配置
        public static int BackpackPaddingH { get; set; } = 8;
        public static int BackpackPaddingTop { get; set; } = 0;
        public static int BackpackContentWidth { get; set; } = 480;
        public static int BackpackAreaHeight { get; set; } = 240;
        public static int BackpackItemSpacing { get; set; } = 8;
        public static bool LockHorizontalResize { get; set; } = true;
        public static bool LockVerticalResize { get; set; } = false;
        public static bool DebugDrawItemBorder { get; set; } = true;
        public static bool DebugShowItemDimensions { get; set; }
        public static int BackpackHoverCornerRadius { get; set; } = 8;
        public static int BackpackHoverBoxBorderWidth { get; set; } = 2;
        public static Color BackpackHoverBoxColor { get; set; } = Colors.White;
        public static float BackpackReorderAnimationDuration { get; set; } = 0.15f;

        protected override void OnPanelInitialized()
        {
            _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            if (_content == null)
                return;

            // 应用缩放锁：锁定对应方向后 DraggablePanel 不再响应该方向缩放
            EnableHorizontalResize = !LockHorizontalResize;
            EnableVerticalResize = !LockVerticalResize;

            AddToGroup("inventory_ui");

            BuildContent();
            _initialPlacementPending = true;
            ResizeToContent();

            var inventoryManager = UiServices.GetInventoryManager(this);
            if (inventoryManager != null)
                inventoryManager.InventoryChanged += OnInventoryChanged;

            SetToggleKey(Key.I);
            RefreshInventory();
        }

        /// <summary>
        /// 由 DebugPanel 调用：重新应用静态配置并重建布局。
        /// </summary>
        public void ApplyRuntimeConfig()
        {
            if (_content == null) return;

            EnableHorizontalResize = !LockHorizontalResize;
            EnableVerticalResize = !LockVerticalResize;

            if (_inventoryContainer != null)
                _inventoryContainer.ItemRightClicked -= OnItemRightClicked;

            foreach (Node child in _content.GetChildren())
                child.QueueFree();

            _capacityLabel = null;
            _inventoryContainer = null;

            BuildContent();
            RefreshInventory();
            ResizeToContent();
        }

        private void ResizeToContent()
        {
            // 让 PanelContainer 根据内容重新计算一次尺寸
            if (_content != null)
            {
                _content.QueueSort();
                (_content.GetParent() as Container)?.QueueSort();
            }

            // 等一帧让容器布局完成后再固定尺寸
            CallDeferred(nameof(FinalizePanelSize));
        }

        private void FinalizePanelSize()
        {
            if (_content == null) return;

            // 用内容的自然尺寸加上 PanelContainer 自身边距，得到根节点尺寸
            var contentSize = _content.GetCombinedMinimumSize();
            var panelStyle = GetThemeStylebox("panel");
            var panelMarginH = (panelStyle?.ContentMarginLeft ?? 0f) + (panelStyle?.ContentMarginRight ?? 0f);
            var panelMarginV = (panelStyle?.ContentMarginTop ?? 0f) + (panelStyle?.ContentMarginBottom ?? 0f);

            var rootWidth = contentSize.X + panelMarginH;
            var rootMinHeight = contentSize.Y + panelMarginV;

            // 只调整宽度；高度保持当前值（首次或手动缩放后的值），不受背包区高度滑条影响
            CustomMinimumSize = new Vector2(rootWidth, rootMinHeight);
            Size = new Vector2(rootWidth, Size.Y);

            // 只有首次初始化时才自动居中，后续由 DebugPanel 调整布局时保持原位
            if (_initialPlacementPending)
            {
                CenterOnViewport();
                _initialPlacementPending = false;
            }
        }

        public override void _ExitTree()
        {
            var inventoryManager = UiServices.GetInventoryManager(this);
            if (inventoryManager != null)
                inventoryManager.InventoryChanged -= OnInventoryChanged;

            base._ExitTree();
        }

        protected internal override void NotifyFocusGained()
        {
            RefreshInventory();
        }

        private void CenterOnViewport()
        {
            var viewportSize = GetViewport().GetVisibleRect().Size;
            Position = new Vector2((viewportSize.X - Size.X) / 2, (viewportSize.Y - Size.Y) / 2);
        }
    }
}
