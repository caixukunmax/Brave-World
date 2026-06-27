using GameServer.Common.Inventory;
using GameServer.Database.Models;
using GameServer.Database.Repositories;
using GameServer.Services.Core;
using GameServer.Tables;
using PGame = global::Game;

namespace GameServer.GameLogic.Inventory;

/// <summary>
/// 背包容量与显示拆分辅助类。
/// 把数据库聚合数据按 max_pile_num 拆分，并提供文字长度容量校验。
/// </summary>
public static class InventoryHelper
{
    /// <summary>
    /// 将数据库聚合数据转换为显示条目（按 max_pile_num 拆分）。
    /// </summary>
    public static List<TextInventoryLayout.ItemEntry> BuildDisplayItems(List<InventoryItem> dbItems, LubanTableLoader tables)
    {
        var result = new List<TextInventoryLayout.ItemEntry>();
        foreach (var dbItem in dbItems)
        {
            var cfg = tables.GetItem(dbItem.ItemId);
            if (cfg == null)
                continue;

            int maxPile = cfg.MaxPileNum > 0 ? cfg.MaxPileNum : int.MaxValue;
            result.AddRange(TextInventoryLayout.SplitIntoDisplayEntries(
                (uint)dbItem.ItemId,
                cfg.Name,
                dbItem.Count,
                cfg.Quality,
                maxPile));
        }
        return result;
    }

    /// <summary>
    /// 将数据库聚合数据转换为 Protobuf ItemInfo 列表（已按 max_pile_num 拆分）。
    /// </summary>
    public static List<PGame.ItemInfo> BuildItemsProto(List<InventoryItem> dbItems, LubanTableLoader tables)
    {
        var displayItems = BuildDisplayItems(dbItems, tables);
        var result = new List<PGame.ItemInfo>();
        foreach (var item in displayItems)
            result.Add(new PGame.ItemInfo { ItemId = item.ItemId, Count = item.Count });
        return result;
    }

    /// <summary>
    /// 将数据库聚合数据转换为 Protobuf ItemInfo 列表（先按 order 排序，再按 max_pile_num 拆分）。
    /// </summary>
    public static List<PGame.ItemInfo> BuildItemsProto(List<InventoryItem> dbItems, LubanTableLoader tables, List<int> order)
    {
        var orderedItems = InventoryOrderHelper.ApplyOrder(dbItems, order);
        return BuildItemsProto(orderedItems, tables);
    }

    /// <summary>
    /// 从仓库读取并转换。
    /// </summary>
    public static async Task<List<PGame.ItemInfo>> BuildItemsProto(InventoryRepository inventory, long roleId, LubanTableLoader tables)
    {
        var dbItems = await inventory.GetByRole(roleId);
        return BuildItemsProto(dbItems, tables);
    }

    /// <summary>
    /// 计算可放入背包的实际数量。
    /// </summary>
    /// <returns>(实际可放入数量, 剩余数量)</returns>
    public static (int added, int remaining) CalculatePickupCapacity(
        List<InventoryItem> currentItems,
        LubanTableLoader tables,
        int itemId,
        int addCount)
    {
        if (addCount <= 0)
            return (0, 0);

        var cfg = tables.GetItem(itemId);
        if (cfg == null)
            return (0, addCount);

        int maxPile = cfg.MaxPileNum > 0 ? cfg.MaxPileNum : int.MaxValue;
        var displayItems = BuildDisplayItems(currentItems, tables);

        return TextInventoryLayout.CalculatePickupCapacity(
            displayItems,
            (uint)itemId,
            cfg.Name,
            addCount,
            cfg.Quality,
            maxPile,
            GameConstants.InventoryLineWidth,
            GameConstants.InventoryTotalCapacity,
            GameConstants.InventorySpacingWidth);
    }
}
