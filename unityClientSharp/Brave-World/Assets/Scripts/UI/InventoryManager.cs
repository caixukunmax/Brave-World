using System;
using System.Collections.Generic;
using UnityClientSharp.Entity;
using UnityClientSharp.Net;
using UnityEngine;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 背包数据层 — 移植自 clinetcsharp/Scripts/InventoryManager.cs。
    /// 职责：物品列表 + InventoryChanged 事件；使用/丢弃/整理请求的发送与响应处理；
    /// 拾取广播（DropPickupNotify，服务端向全图广播）按拾取者过滤后的本地合并。
    /// 与 Godot 版差异：物品名/品质不再自解析 item_config.json，直接复用 <see cref="ItemIconCatalog"/>
    /// （同一份 Luban 配置，语义与 Godot GetItemName/GetItemQuality 一致，含 "物品{id}" 兜底）。
    /// 离线降级（AGENTS.md 第 18 条）：无 NetworkManager 或未连接时所有发送静默返回，不计数不报警。
    /// 构造即订阅（先于任何发送，AGENTS.md 第 16 条）；缓存已有背包数据直接重放（第 21/22 条）。
    /// </summary>
    public class InventoryManager
    {
        /// <summary>背包内容变化（含整理失败触发的回滚重建，对齐 Godot 失败分支也发信号）。</summary>
        public event Action InventoryChanged;

        public class ItemSlot
        {
            public uint ItemId;
            public uint Count;
            public string Name = "";
        }

        public List<ItemSlot> Items { get; private set; } = new List<ItemSlot>();

        private readonly NetworkManager _network;

        public InventoryManager()
        {
            _network = NetworkManager.Instance;
            if (_network == null) return;

            _network.UseItemResponse += OnUseItemResponse;
            _network.DropItemResponse += OnDropItemResponse;
            _network.DropPickupNotify += OnDropPickupNotify;
            _network.InventoryReorderResponse += OnInventoryReorderResponse;

            // 管理器可能晚于背包数据创建：缓存非空直接用缓存渲染，不干等事件
            if (_network.CachedItems.Count > 0)
                UpdateFromProto(_network.CachedItems);
        }

        public void Dispose()
        {
            if (_network == null) return;
            _network.UseItemResponse -= OnUseItemResponse;
            _network.DropItemResponse -= OnDropItemResponse;
            _network.DropPickupNotify -= OnDropPickupNotify;
            _network.InventoryReorderResponse -= OnInventoryReorderResponse;
        }

        public void UpdateItems(List<ItemSlot> newItems)
        {
            Items = newItems;
            InventoryChanged?.Invoke();
        }

        /// <summary>通过 ItemInfo 列表全量更新背包。</summary>
        public void UpdateFromProto(IReadOnlyList<Game.ItemInfo> protoItems)
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
            InventoryChanged?.Invoke();
        }

        /// <summary>
        /// 将新物品合并到现有背包中（累加数量或新增条目），用于开箱/拾取等只返回增量物品的场景。
        /// </summary>
        public void AddOrUpdateItemsFromProto(IReadOnlyList<Game.ItemInfo> protoItems)
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
            InventoryChanged?.Invoke();
        }

        public void SendUseItem(uint itemId, uint count)
        {
            if (_network == null || !_network.IsServerConnected()) return;
            _network.SendPacket(Protocol.MessageId.GameUseItemReq, new Game.UseItemRequest { ItemId = itemId, Count = count });
        }

        public void SendDropItem(uint itemId, uint count)
        {
            if (_network == null || !_network.IsServerConnected()) return;
            _network.SendPacket(Protocol.MessageId.GameDropItemReq, new Game.DropItemRequest { ItemId = itemId, Count = count });
        }

        public void SendReorderItems(List<uint> orderedItemIds)
        {
            if (_network == null || !_network.IsServerConnected()) return;
            var req = new Game.InventoryReorderRequest();
            foreach (var id in orderedItemIds)
                req.OrderedItemIds.Add(id);
            _network.SendPacket(Protocol.MessageId.GameInventoryReorderReq, req);
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

        private void OnInventoryReorderResponse(Game.InventoryReorderResponse rsp)
        {
            if (rsp.Code == Common.ErrorCode.Success)
            {
                UpdateFromProto(rsp.Items);
                // NetworkManager 的 reorder 分发不写缓存（与 Use/Drop 不同），这里补齐保持一致
                _network.CachedItems = new List<Game.ItemInfo>(rsp.Items);
            }
            else
            {
                Debug.LogWarning($"[InventoryManager] reorder failed: {rsp.Message}");
                // 触发 InventoryChanged 让面板按旧顺序重建，完成本地预览回滚（对齐 Godot 注释：
                // 调用方 TextInventoryContainer 负责在失败时回滚本地预览）
                InventoryChanged?.Invoke();
            }
        }

        private void OnDropPickupNotify(Game.DropPickupNotify notify)
        {
            // 只有自己是拾取者时才更新背包（服务端广播给全地图玩家）
            if (notify.PickerId != _network.AccountId || notify.ActualCount == 0)
                return;

            var items = new List<Game.ItemInfo>
            {
                new Game.ItemInfo { ItemId = notify.ItemId, Count = notify.ActualCount }
            };
            AddOrUpdateItemsFromProto(items);
        }

        public string GetItemName(uint itemId) => ItemIconCatalog.GetItemName((int)itemId);

        public int GetItemQuality(uint itemId) => ItemIconCatalog.GetQuality((int)itemId);
    }
}
