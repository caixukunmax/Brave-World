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

        // 物品配置：id → { name, desc, max_pile, quality, icon }
        private class ItemConfig
        {
            public string Name = "";
            public string Desc = "";
            public int MaxPile = 99;
            public int Quality = 0;
            public string Icon = "";
        }

        public List<ItemSlot> Items { get; private set; } = new List<ItemSlot>();
        private Dictionary<uint, ItemConfig> _itemConfig = new Dictionary<uint, ItemConfig>();
        private NetworkManager _network;

        public override void _Ready()
        {
            AddToGroup("inventory_manager");
            LoadItemConfig();

            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (_network != null)
            {
                _network.UseItemResponse += OnUseItemResponse;
                _network.DropItemResponse += OnDropItemResponse;

                if (_network.CachedItems.Count > 0)
                {
                    var items = new Google.Protobuf.Collections.RepeatedField<Game.ItemInfo>();
                    items.AddRange(_network.CachedItems);
                    UpdateFromProto(items);
                }
            }
        }

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.UseItemResponse -= OnUseItemResponse;
                _network.DropItemResponse -= OnDropItemResponse;
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

            // Luban 格式：数组 [{ id, name, major_type, minor_type, max_pile_num, quality, icon, icon_backgroud, icon_mask, desc, show_order, effect_type, effect_value, price, can_sell, obtain_methods, release_date }, ...]
            var array = json.Data.AsGodotArray();
            foreach (var entry in array)
            {
                var dict = entry.AsGodotDictionary();
                var id = (uint)dict["id"].AsInt32();
                _itemConfig[id] = new ItemConfig
                {
                    Name = dict.ContainsKey("name") ? dict["name"].AsString() : "",
                    Desc = dict.ContainsKey("desc") ? dict["desc"].AsString() : "",
                    MaxPile = dict.ContainsKey("max_pile_num") ? dict["max_pile_num"].AsInt32() : 99,
                    Quality = dict.ContainsKey("quality") ? dict["quality"].AsInt32() : 0,
                    Icon = dict.ContainsKey("icon") ? dict["icon"].AsString() : "",
                };
            }
            GD.Print($"[InventoryManager] Loaded {_itemConfig.Count} item configs from Luban table");
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
            if (_network == null || !_network.IsServerConnected()) return;
            var req = new Game.UseItemRequest { ItemId = itemId, Count = count };
            _network.SendPacket(MessageId.GameUseItemReq, req);
        }

        public void SendDropItem(uint itemId, uint count)
        {
            if (_network == null || !_network.IsServerConnected()) return;
            var req = new Game.DropItemRequest { ItemId = itemId, Count = count };
            _network.SendPacket(MessageId.GameDropItemReq, req);
        }

        private void OnUseItemResponse(Game.UseItemResponse rsp)
        {
            if (rsp.Code == Common.ErrorCode.Success)
                UpdateFromProto(rsp.Items);
        }

        private void OnDropItemResponse(Game.DropItemResponse rsp)
        {
            if (rsp.Code == Common.ErrorCode.Success)
                UpdateFromProto(rsp.Items);
        }

        public string GetItemName(uint itemId)
        {
            if (_itemConfig.TryGetValue(itemId, out var cfg))
                return cfg.Name;
            return $"物品{itemId}";
        }

        public string GetItemIcon(uint itemId)
        {
            if (_itemConfig.TryGetValue(itemId, out var cfg))
                return cfg.Icon;
            return string.Empty;
        }

        public int GetItemQuality(uint itemId)
        {
            if (_itemConfig.TryGetValue(itemId, out var cfg))
                return cfg.Quality;
            return 0;
        }
    }
}
