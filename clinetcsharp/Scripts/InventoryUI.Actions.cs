using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    public partial class InventoryUI
    {
        private void OnInventoryChanged()
        {
            RefreshInventory();
        }

        private void RefreshInventory()
        {
            if (_inventoryContainer == null)
                return;

            var manager = UiServices.GetInventoryManager(this);
            if (manager == null)
            {
                _inventoryContainer.SetItems(new List<TextInventoryLayout.ItemEntry>());
                UpdateCapacityLabel();
                return;
            }

            var entries = new List<TextInventoryLayout.ItemEntry>();
            foreach (var slot in manager.Items)
            {
                entries.Add(new TextInventoryLayout.ItemEntry
                {
                    ItemId = slot.ItemId,
                    Name = slot.Name,
                    Count = slot.Count,
                    Quality = manager.GetItemQuality(slot.ItemId),
                });
            }

            _inventoryContainer.SetItems(entries);
            UpdateCapacityLabel();
        }

        private void OnItemRightClicked(uint itemId, uint count)
        {
            var popup = new PopupMenu();
            popup.AddItem("使用 x1", 0);
            popup.AddItem("丢弃 x1", 1);
            popup.AddItem("丢弃全部", 2);

            popup.IdPressed += id =>
            {
                var manager = UiServices.GetInventoryManager(this);
                if (manager == null)
                    return;

                if (id == 0)
                    manager.SendUseItem(itemId, 1);
                else if (id == 1)
                    manager.SendDropItem(itemId, 1);
                else if (id == 2)
                    manager.SendDropItem(itemId, count);

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
