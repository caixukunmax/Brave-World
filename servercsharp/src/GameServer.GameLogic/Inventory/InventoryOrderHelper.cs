using GameServer.Database.Models;

namespace GameServer.GameLogic.Inventory;

/// <summary>
/// 背包排序辅助类。
/// 根据客户端拖拽/整理后的顺序对数据库聚合条目进行重排，并维护顺序列表。
/// </summary>
public static class InventoryOrderHelper
{
    /// <summary>
    /// 按 <paramref name="order"/> 指定的顺序重排 <paramref name="items"/>。
    /// 未知条目（未在 order 中出现）按原始相对顺序追加到末尾。
    /// </summary>
    public static List<InventoryItem> ApplyOrder(List<InventoryItem> items, List<int> order)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(order);

        var result = new List<InventoryItem>(items.Count);
        var placed = new HashSet<InventoryItem>();

        // 按 ItemId 分组，保持每组内部的原始顺序。
        var byItemId = new Dictionary<int, Queue<InventoryItem>>();
        foreach (var item in items)
        {
            if (!byItemId.TryGetValue(item.ItemId, out var queue))
            {
                queue = new Queue<InventoryItem>();
                byItemId[item.ItemId] = queue;
            }

            queue.Enqueue(item);
        }

        // 按 order 依次取出对应条目。
        foreach (int itemId in order)
        {
            if (byItemId.TryGetValue(itemId, out var queue))
            {
                while (queue.Count > 0)
                {
                    var item = queue.Dequeue();
                    if (placed.Add(item))
                    {
                        result.Add(item);
                    }
                }
            }
        }

        // 追加未知条目，保留原始相对顺序。
        foreach (var item in items)
        {
            if (placed.Add(item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// 将道具 ID 追加到顺序列表末尾（如果不存在）。
    /// </summary>
    public static void AppendItem(List<int> order, int itemId)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!order.Contains(itemId))
        {
            order.Add(itemId);
        }
    }

    /// <summary>
    /// 从顺序列表中移除指定道具 ID（如果存在）。
    /// </summary>
    public static void RemoveItem(List<int> order, int itemId)
    {
        ArgumentNullException.ThrowIfNull(order);

        order.Remove(itemId);
    }
}
