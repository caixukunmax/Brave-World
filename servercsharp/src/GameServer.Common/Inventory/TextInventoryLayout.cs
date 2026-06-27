namespace GameServer.Common.Inventory;

/// <summary>
/// 文字背包布局计算工具。
/// 与客户端 ClinetCSharp.TextInventoryLayout 保持算法一致。
/// </summary>
public static class TextInventoryLayout
{
    public const float ChineseCharWidth = 1.0f;
    public const float MultiplierWidth = 1.0f;
    public const float DigitWidth = 0.6f;
    public const float SpacingWidth = 0.5f;

    public const int DefaultLineWidth = 30;
    public const int DefaultLineCount = 10;
    public const int DefaultTotalCapacity = DefaultLineWidth * DefaultLineCount;

    public class ItemEntry
    {
        public uint ItemId { get; set; }
        public string Name { get; set; } = "";
        public uint Count { get; set; }
        public int Quality { get; set; }
    }

    /// <summary>
    /// 计算单个道具条目的显示宽度（单位：x）。
    /// 宽度 = 名字字数 + 1（乘号）+ 数字位数 × 0.6。
    /// </summary>
    public static float MeasureItemWidth(string name, uint count)
    {
        ArgumentNullException.ThrowIfNull(name);

        int digits = count == 0 ? 1 : (int)Math.Floor(Math.Log10(count)) + 1;
        return name.Length * ChineseCharWidth + MultiplierWidth + digits * DigitWidth;
    }

    /// <summary>
    /// 按固定行宽对道具条目进行流式重排。
    /// </summary>
    public static List<List<ItemEntry>> Reflow(List<ItemEntry> items, float lineWidth, float spacingWidth = SpacingWidth)
    {
        var lines = new List<List<ItemEntry>>();
        var currentLine = new List<ItemEntry>();
        float currentWidth = 0f;

        foreach (var item in items)
        {
            float itemWidth = MeasureItemWidth(item.Name, item.Count);
            if (itemWidth > lineWidth)
                throw new InvalidOperationException($"Item '{item.Name}' is too wide for the inventory panel.");

            if (currentLine.Count > 0)
                itemWidth += spacingWidth;

            if (currentWidth + itemWidth > lineWidth)
            {
                lines.Add(currentLine);
                currentLine = new List<ItemEntry>();
                currentWidth = 0f;
                itemWidth = MeasureItemWidth(item.Name, item.Count);
            }

            currentLine.Add(item);
            currentWidth += itemWidth;
        }

        if (currentLine.Count > 0)
            lines.Add(currentLine);

        return lines;
    }

    /// <summary>
    /// 判断当前道具是否能在给定尺寸下放下。
    /// </summary>
    public static bool CanFit(List<ItemEntry> items, float lineWidth, int lineCount, float spacingWidth = SpacingWidth)
    {
        try
        {
            var lines = Reflow(items, lineWidth, spacingWidth);
            return lines.Count <= lineCount;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// 计算显示条目的总占用宽度（x 单位），按 Reflow 后的实际排列求和。
    /// </summary>
    public static float CalculateUsedCapacity(List<ItemEntry> items, float lineWidth = DefaultLineWidth, float spacingWidth = SpacingWidth)
    {
        try
        {
            var lines = Reflow(items, lineWidth, spacingWidth);
            float used = 0f;
            foreach (var line in lines)
            {
                for (int i = 0; i < line.Count; i++)
                {
                    used += (float)Math.Ceiling(MeasureItemWidth(line[i].Name, line[i].Count));
                    if (i < line.Count - 1)
                        used += (float)Math.Ceiling(spacingWidth);
                }
            }
            return used;
        }
        catch (InvalidOperationException)
        {
            return DefaultTotalCapacity;
        }
    }

    /// <summary>
    /// 按最大堆叠数将聚合后的数量拆分为多条显示条目。
    /// </summary>
    public static List<ItemEntry> SplitIntoDisplayEntries(uint itemId, string name, int totalCount, int quality, int maxPileNum)
    {
        var entries = new List<ItemEntry>();
        if (totalCount <= 0 || maxPileNum <= 0)
            return entries;

        int remaining = totalCount;
        while (remaining > 0)
        {
            int pile = Math.Min(remaining, maxPileNum);
            entries.Add(new ItemEntry
            {
                ItemId = itemId,
                Name = name,
                Count = (uint)pile,
                Quality = quality,
            });
            remaining -= pile;
        }

        return entries;
    }

    /// <summary>
    /// 将聚合后的背包数据（按 item_id 唯一）拆分为显示条目列表。
    /// </summary>
    public static List<ItemEntry> BuildDisplayItems(
        IEnumerable<(uint itemId, string name, int totalCount, int quality)> aggregatedItems,
        Func<uint, int> getMaxPileNum)
    {
        var result = new List<ItemEntry>();
        foreach (var (itemId, name, totalCount, quality) in aggregatedItems)
        {
            int maxPile = getMaxPileNum(itemId);
            result.AddRange(SplitIntoDisplayEntries(itemId, name, totalCount, quality, maxPile));
        }
        return result;
    }

    /// <summary>
    /// 计算向当前背包中添加指定数量物品后，实际能放入的数量。
    /// 返回 (addedCount, remainingCount)。
    /// </summary>
    public static (int added, int remaining) CalculatePickupCapacity(
        List<ItemEntry> currentDisplayItems,
        uint itemId,
        string itemName,
        int addCount,
        int quality,
        int maxPileNum,
        float lineWidth = DefaultLineWidth,
        int totalCapacity = DefaultTotalCapacity,
        float spacingWidth = SpacingWidth)
    {
        if (addCount <= 0)
            return (0, 0);
        if (maxPileNum <= 0)
            return (0, addCount);

        // 单条宽度检查：如果该道具单个条目（即使数量为1）都放不下，则完全无法放入
        if (MeasureItemWidth(itemName, 1) > lineWidth)
            return (0, addCount);

        // 二分查找最大可放入数量
        int low = 0;
        int high = addCount;
        while (low < high)
        {
            int mid = low + (high - low + 1) / 2;
            if (CanFitAfterAdding(currentDisplayItems, itemId, itemName, mid, quality, maxPileNum, lineWidth, totalCapacity, spacingWidth))
                low = mid;
            else
                high = mid - 1;
        }

        return (low, addCount - low);
    }

    private static bool CanFitAfterAdding(
        List<ItemEntry> currentDisplayItems,
        uint itemId,
        string itemName,
        int addCount,
        int quality,
        int maxPileNum,
        float lineWidth,
        int totalCapacity,
        float spacingWidth)
    {
        var afterAdding = BuildItemsAfterAdding(currentDisplayItems, itemId, itemName, addCount, quality, maxPileNum);
        return CanFit(afterAdding, lineWidth, totalCapacity / (int)lineWidth, spacingWidth);
    }

    private static List<ItemEntry> BuildItemsAfterAdding(
        List<ItemEntry> currentDisplayItems,
        uint itemId,
        string itemName,
        int addCount,
        int quality,
        int maxPileNum)
    {
        var result = new List<ItemEntry>();

        // 复制非该 item 的条目
        foreach (var item in currentDisplayItems)
        {
            if (item.ItemId != itemId)
                result.Add(new ItemEntry { ItemId = item.ItemId, Name = item.Name, Count = item.Count, Quality = item.Quality });
        }

        // 计算该 item 的新总数量并拆分
        int existingCount = currentDisplayItems
            .Where(i => i.ItemId == itemId)
            .Sum(i => (int)i.Count);
        int newTotal = existingCount + addCount;

        result.AddRange(SplitIntoDisplayEntries(itemId, itemName, newTotal, quality, maxPileNum));
        return result;
    }
}
