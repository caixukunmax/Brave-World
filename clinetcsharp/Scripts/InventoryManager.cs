using Godot;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 背包管理器 - 管理玩家背包数据，处理服务器通信
    /// </summary>
    [GlobalClass]
    public partial class InventoryManager : Node
    {
        [Signal]
        public delegate void InventoryChangedEventHandler();

        public class ItemSlot
        {
            public uint ItemId;
            public uint Count;
            public string Name = "";
        }

        // 物品配置：id → { name, desc, max_pile, quality }
        private class ItemConfig
        {
            public string Name = "";
            public string Desc = "";
            public int MaxPile = 99;
            public int Quality = 0;
        }

        public List<ItemSlot> Items { get; private set; } = new List<ItemSlot>();
        private Dictionary<uint, ItemConfig> _itemConfig = new Dictionary<uint, ItemConfig>();

        public override void _Ready()
        {
            AddToGroup("inventory_manager");
            GD.Print("[InventoryManager] _Ready() called");
            LoadItemConfig();

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
            {
                nm.PacketReceived += OnPacketReceived;
                GD.Print("[InventoryManager] Connected to NetworkManager");
            }
            else
            {
                GD.PrintErr("[InventoryManager] NetworkManager not found!");
            }
        }

        private void LoadItemConfig()
        {
            var file = FileAccess.Open("res://data/item_config.json", FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr("[InventoryManager] item_config.json not found");
                return;
            }
            var jsonText = file.GetAsText();
            file.Close();

            var json = new Godot.Json();
            var err = json.Parse(jsonText);
            if (err != Error.Ok)
            {
                GD.PrintErr("[InventoryManager] Failed to parse item_config.json");
                return;
            }

            var data = json.Data.AsGodotDictionary();
            foreach (var key in data.Keys)
            {
                var id = (uint)key.AsInt32();
                var entry = data[key].AsGodotDictionary();
                _itemConfig[id] = new ItemConfig
                {
                    Name = entry.ContainsKey("name") ? entry["name"].AsString() : "",
                    Desc = entry.ContainsKey("desc") ? entry["desc"].AsString() : "",
                    MaxPile = entry.ContainsKey("max_pile") ? entry["max_pile"].AsInt32() : 99,
                    Quality = entry.ContainsKey("quality") ? entry["quality"].AsInt32() : 0,
                };
            }
            GD.Print($"[InventoryManager] Loaded {_itemConfig.Count} item configs");
        }

        /// <summary>
        /// 从服务器响应更新背包（进入游戏/创建角色时调用）
        /// </summary>
        public void LoadFromServer(Game.FullRoleInfo roleInfo)
        {
            // 进入游戏时背包通过 NetworkManager.CacheResponse 处理
        }

        public void UpdateItems(List<ItemSlot> newItems)
        {
            Items = newItems;
            EmitSignal(SignalName.InventoryChanged);
        }

        /// <summary>
        /// 通过 ItemInfo 列表更新背包
        /// </summary>
        public void UpdateFromProto(RepeatedField<Game.ItemInfo> protoItems)
        {
            var newItems = new List<ItemSlot>();
            foreach (var item in protoItems)
            {
                newItems.Add(new ItemSlot
                {
                    ItemId = item.ItemId,
                    Count = item.Count,
                    Name = GetItemName(item.ItemId),
                });
            }
            Items = newItems;
            EmitSignal(SignalName.InventoryChanged);
        }

        /// <summary>
        /// 将新物品合并到现有背包中（累加数量或新增条目），用于开箱等只返回增量物品的场景
        /// </summary>
        public void AddOrUpdateItemsFromProto(RepeatedField<Game.ItemInfo> protoItems)
        {
            foreach (var item in protoItems)
            {
                bool found = false;
                foreach (var slot in Items)
                {
                    if (slot.ItemId == item.ItemId)
                    {
                        slot.Count += item.Count;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Items.Add(new ItemSlot
                    {
                        ItemId = item.ItemId,
                        Count = item.Count,
                        Name = GetItemName(item.ItemId),
                    });
                }
            }
            EmitSignal(SignalName.InventoryChanged);
        }

        public void SendUseItem(uint itemId, uint count)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected()) return;

            var req = new Game.UseItemRequest { ItemId = itemId, Count = count };
            nm.SendPacket(MessageId.GameUseItemReq, req);
        }

        public void SendDropItem(uint itemId, uint count)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected()) return;

            var req = new Game.DropItemRequest { ItemId = itemId, Count = count };
            nm.SendPacket(MessageId.GameDropItemReq, req);
        }

        private void OnPacketReceived(int msgId)
        {
            if ((MessageId)msgId == MessageId.GameUseItemRsp)
                HandleUseItemResponse();
            else if ((MessageId)msgId == MessageId.GameDropItemRsp)
                HandleDropItemResponse();
        }

        private void HandleUseItemResponse()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            var payload = nm?.GetLastPayload();
            if (payload == null) return;

            var rsp = Game.UseItemResponse.Parser.ParseFrom(payload);
            GD.Print($"[Inventory] UseItem response: code={rsp.Code}");
            UpdateFromProto(rsp.Items);
        }

        private void HandleDropItemResponse()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            var payload = nm?.GetLastPayload();
            if (payload == null) return;

            var rsp = Game.DropItemResponse.Parser.ParseFrom(payload);
            GD.Print($"[Inventory] DropItem response: code={rsp.Code}");
            UpdateFromProto(rsp.Items);
        }

        public string GetItemName(uint itemId)
        {
            if (_itemConfig.TryGetValue(itemId, out var cfg))
                return cfg.Name;
            return $"物品{itemId}";
        }
    }
}
