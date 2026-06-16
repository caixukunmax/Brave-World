using Xunit;
using System.Collections.Generic;

namespace ClinetCSharp.Tests;

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
            new TextInventoryLayout.ItemEntry { Name = "魔法剑", Count = 1, Quality = 0 }
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
            new TextInventoryLayout.ItemEntry { Name = "这是一个非常长的道具名字", Count = 1, Quality = 0 }
        };
        Assert.Throws<System.InvalidOperationException>(() => TextInventoryLayout.Reflow(items, 10f));
    }

    [Fact]
    public void CanFit_ItemsWithinCapacity_ReturnsTrue()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "魔法剑", Count = 1, Quality = 0 },
            new TextInventoryLayout.ItemEntry { Name = "高精宝石", Count = 30, Quality = 0 }
        };
        Assert.True(TextInventoryLayout.CanFit(items, 30f, 10));
    }
}
