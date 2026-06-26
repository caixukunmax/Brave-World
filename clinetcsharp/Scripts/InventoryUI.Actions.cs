using Godot;

namespace ClinetCSharp
{
    public partial class InventoryUI
    {
        private void OnInventoryChanged()
        {
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            if (_grid == null)
                return;

            foreach (var child in _grid.GetChildren())
                child.QueueFree();

            var inventoryManager = UiServices.GetInventoryManager(this);
            if (inventoryManager == null)
            {
                FillEmptySlots(DefaultMaxSlots);
                return;
            }

            foreach (var slot in inventoryManager.Items)
                _grid.AddChild(BuildItemButton(slot, inventoryManager));

            int emptySlotCount = DefaultMaxSlots - (inventoryManager.Items?.Count ?? 0);
            FillEmptySlots(emptySlotCount);
        }

        private void FillEmptySlots(int count)
        {
            for (int i = 0; i < count; i++)
                _grid.AddChild(BuildEmptySlotButton());
        }

        private void OnItemClicked(uint itemId, uint count)
        {
            var popup = new PopupMenu();
            popup.AddItem("使用 x1", 0);
            popup.AddItem("丢弃 x1", 1);

            popup.IdPressed += id =>
            {
                var inventoryManager = UiServices.GetInventoryManager(this);
                if (inventoryManager == null)
                    return;

                if (id == 0)
                    inventoryManager.SendUseItem(itemId, 1);
                else if (id == 1)
                    inventoryManager.SendDropItem(itemId, 1);

                popup.QueueFree();
            };

            popup.PopupHide += () => popup.QueueFree();

            AddChild(popup);
            popup.Position = new Vector2I(
                Mathf.RoundToInt(GetViewport().GetMousePosition().X),
                Mathf.RoundToInt(GetViewport().GetMousePosition().Y));
            popup.Popup();
        }
    }
}
