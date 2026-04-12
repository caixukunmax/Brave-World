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

        public List<ItemSlot> Items { get; private set; } = new List<ItemSlot>();

        // 物品名称本地缓存（从配置表获取）
        private Dictionary<uint, string> _itemNameCache = new Dictionary<uint, string>();

        public override void _Ready()
        {
            AddToGroup("inventory_manager");

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
                nm.PacketReceived += OnPacketReceived;
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

        private string GetItemName(uint itemId)
        {
            if (_itemNameCache.TryGetValue(itemId, out string name))
                return name;
            return $"物品{itemId}";
        }

        /// <summary>
        /// 供外部设置物品名称缓存
        /// </summary>
        public void SetItemName(uint itemId, string name)
        {
            _itemNameCache[itemId] = name;
        }
    }
}
