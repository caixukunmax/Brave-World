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

            // DEBUG: 临时填充测试道具，方便观察换行、拖拽等效果
            AddDebugItems(entries);

            _inventoryContainer.SetItems(entries);
            UpdateCapacityLabel();
        }

        private void AddDebugItems(List<TextInventoryLayout.ItemEntry> entries)
        {
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9001, Name = "长剑", Count = 1, Quality = 0 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9002, Name = "布衣", Count = 1, Quality = 0 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9003, Name = "草药", Count = 20, Quality = 0 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9004, Name = "法力药水", Count = 5, Quality = 1 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9005, Name = "火把", Count = 12, Quality = 0 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9006, Name = "远古卷轴", Count = 1, Quality = 3 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9007, Name = "精金锭", Count = 99, Quality = 2 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9008, Name = "龙之逆鳞", Count = 1, Quality = 4 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9009, Name = "复活石", Count = 3, Quality = 3 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9010, Name = "传送卷轴", Count = 10, Quality = 1 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9011, Name = "铁镐", Count = 1, Quality = 0 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9012, Name = "魔法书", Count = 1, Quality = 2 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9013, Name = "圣水", Count = 7, Quality = 1 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9014, Name = "盗贼匕首", Count = 1, Quality = 2 });
            entries.Add(new TextInventoryLayout.ItemEntry { ItemId = 9015, Name = "光明护符", Count = 1, Quality = 4 });
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
