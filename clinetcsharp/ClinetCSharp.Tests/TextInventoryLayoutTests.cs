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
    public void MeasureItemWidth_ZeroCount_TreatsAsSingleDigit()
    {
        float width = TextInventoryLayout.MeasureItemWidth("魔法剑", 0);
        Assert.Equal(3f + 1f + 0.6f, width, 3);
    }

    [Fact]
    public void MeasureItemWidth_LargeCount_CalculatesDigitsCorrectly()
    {
        float width = TextInventoryLayout.MeasureItemWidth("药", 1000);
        int digits = 4;
        Assert.Equal(1f + 1f + digits * 0.6f, width, 3);
    }

    [Fact]
    public void MeasureItemWidth_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() => TextInventoryLayout.MeasureItemWidth(null!, 1));
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
    public void Reflow_EmptyList_ReturnsEmptyLines()
    {
        var items = new List<TextInventoryLayout.ItemEntry>();
        var lines = TextInventoryLayout.Reflow(items, 30f);
        Assert.Empty(lines);
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
    public void Reflow_ExactWidthBoundary_FitsOnOneLine()
    {
        // "剑" (1) + "×" (1) + "1" (0.6) = 2.6
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "剑", Count = 1, Quality = 0 }
        };
        var lines = TextInventoryLayout.Reflow(items, 2.6f);
        Assert.Single(lines);
        Assert.Single(lines[0]);
    }

    [Fact]
    public void Reflow_TwoItemsWithSpacing_SplitsWhenSpacingWouldExceedWidth()
    {
        // Each item: 1 + 1 + 0.6 = 2.6. Two items with spacing: 2.6 + 0.5 + 2.6 = 5.7
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "剑", Count = 1, Quality = 0 },
            new TextInventoryLayout.ItemEntry { Name = "盾", Count = 1, Quality = 0 }
        };
        var lines = TextInventoryLayout.Reflow(items, 5.6f);
        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void Reflow_TwoItemsFitWithSpacing_StaysOnOneLine()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "剑", Count = 1, Quality = 0 },
            new TextInventoryLayout.ItemEntry { Name = "盾", Count = 1, Quality = 0 }
        };
        var lines = TextInventoryLayout.Reflow(items, 5.7f);
        Assert.Single(lines);
        Assert.Equal(2, lines[0].Count);
    }

    [Fact]
    public void Reflow_MultiLine_WrapsToNextLine()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "剑", Count = 1, Quality = 0 },
            new TextInventoryLayout.ItemEntry { Name = "盾", Count = 1, Quality = 0 },
            new TextInventoryLayout.ItemEntry { Name = "斧", Count = 1, Quality = 0 }
        };
        // Two single-char items with spacing: 2.6 + 0.5 + 2.6 = 5.7, line width 6 fits two but wraps third
        var lines = TextInventoryLayout.Reflow(items, 6f);
        Assert.Equal(2, lines.Count);
        Assert.Equal(2, lines[0].Count);
        Assert.Single(lines[1]);
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

    [Fact]
    public void CanFit_ItemsExceedLineCount_ReturnsFalse()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "魔法剑", Count = 1, Quality = 0 },
            new TextInventoryLayout.ItemEntry { Name = "高精宝石", Count = 30, Quality = 0 },
            new TextInventoryLayout.ItemEntry { Name = "生命药水", Count = 5, Quality = 0 }
        };
        Assert.False(TextInventoryLayout.CanFit(items, 8f, 1));
    }

    [Fact]
    public void CanFit_ItemTooWide_ReturnsFalse()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "这是一个非常长的道具名字", Count = 1, Quality = 0 }
        };
        Assert.False(TextInventoryLayout.CanFit(items, 10f, 10));
    }
}
