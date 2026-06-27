using GameServer.Common.Inventory;
using Xunit;

namespace GameServer.Tests;

public class TextInventoryLayoutTests
{
    [Fact]
    public void MeasureItemWidth_ChineseNameAndCount_ReturnsExpected()
    {
        float width = TextInventoryLayout.MeasureItemWidth("魔法剑", 1);
        Assert.Equal(3f + 1f + 0.6f, width, 3);
    }

    [Fact]
    public void MeasureItemWidth_TwoDigitCount_AddsCorrectDigitWidth()
    {
        float width = TextInventoryLayout.MeasureItemWidth("高精宝石", 30);
        Assert.Equal(4f + 1f + 1.2f, width, 3);
    }

    [Fact]
    public void Reflow_SingleItemFits_ReturnsOneLine()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new() { Name = "魔法剑", Count = 1, Quality = 0 }
        };
        var lines = TextInventoryLayout.Reflow(items, 30f);
        Assert.Single(lines);
        Assert.Single(lines[0]);
    }

    [Fact]
    public void Reflow_ItemExceedsWidth_ThrowsInvalidOperation()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new() { Name = "这是一个非常长的道具名字", Count = 1, Quality = 0 }
        };
        Assert.Throws<InvalidOperationException>(() => TextInventoryLayout.Reflow(items, 10f));
    }

    [Fact]
    public void CanFit_ItemsWithinCapacity_ReturnsTrue()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new() { Name = "魔法剑", Count = 1, Quality = 0 },
            new() { Name = "高精宝石", Count = 30, Quality = 0 }
        };
        Assert.True(TextInventoryLayout.CanFit(items, 30f, 10));
    }

    [Fact]
    public void SplitIntoDisplayEntries_ExceedsMaxPile_SplitsCorrectly()
    {
        var entries = TextInventoryLayout.SplitIntoDisplayEntries(1, "生命药水", 150, 0, 99);
        Assert.Equal(2, entries.Count);
        Assert.Equal(99u, entries[0].Count);
        Assert.Equal(51u, entries[1].Count);
    }

    [Fact]
    public void CalculatePickupCapacity_EmptyInventory_FitsFullCount()
    {
        var current = new List<TextInventoryLayout.ItemEntry>();
        var (added, remaining) = TextInventoryLayout.CalculatePickupCapacity(
            current, 1, "草药", 10, 0, 99);
        Assert.Equal(10, added);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void CalculatePickupCapacity_FullInventory_ReturnsZero()
    {
        // 用宽度接近 30x 的道具填满 300 容量（10 行，每行 1 条）
        var current = new List<TextInventoryLayout.ItemEntry>();
        for (int i = 0; i < 10; i++)
        {
            current.Add(new TextInventoryLayout.ItemEntry
            {
                ItemId = (uint)(100 + i),
                Name = new string('a', 28), // 28 + 1(×) + 0.6(1位数字) ≈ 29.6x
                Count = 1,
                Quality = 0,
            });
        }

        var (added, remaining) = TextInventoryLayout.CalculatePickupCapacity(
            current, 1, "新道具", 5, 0, 99);
        Assert.Equal(0, added);
        Assert.Equal(5, remaining);
    }

    [Fact]
    public void CalculatePickupCapacity_PartialFill_ReturnsPartial()
    {
        // 背包几乎塞满（9 行，每行 1 条接近 30x 的道具）
        var current = new List<TextInventoryLayout.ItemEntry>();
        for (int i = 0; i < 9; i++)
        {
            current.Add(new TextInventoryLayout.ItemEntry
            {
                ItemId = (uint)(100 + i),
                Name = new string('a', 28),
                Count = 1,
                Quality = 0,
            });
        }

        // 一次性尝试放入远超剩余空间的数量
        var (added, remaining) = TextInventoryLayout.CalculatePickupCapacity(
            current, 1, "新道具", 1000, 0, 99);

        Assert.True(added > 0, "应该能放入部分物品");
        Assert.True(remaining > 0, "应该有剩余");
        Assert.Equal(1000, added + remaining);
    }
}
