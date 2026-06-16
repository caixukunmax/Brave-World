using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// Inventory panel shell and lifecycle.
    /// Grid construction and interactions live in partial files.
    /// </summary>
    public partial class InventoryUI : DraggablePanel
    {
        private VBoxContainer _content;
        private GridContainer _grid;

        private const int DefaultMaxSlots = 20;

        protected override void OnPanelInitialized()
        {
            _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            if (_content == null)
                return;

            CenterOnViewport();
            BuildContent();

            var inventoryManager = UiServices.GetInventoryManager(this);
            if (inventoryManager != null)
                inventoryManager.InventoryChanged += OnInventoryChanged;

            SetToggleKey(Key.I);
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
