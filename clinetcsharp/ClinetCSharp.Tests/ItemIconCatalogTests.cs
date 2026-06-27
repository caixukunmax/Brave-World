using Godot;
using Xunit;

namespace ClinetCSharp.Tests;

public class ItemIconCatalogTests
{
    [Theory]
    [InlineData(0, 1f, 1f, 1f)]       // Common - white
    [InlineData(1, 0.1f, 1f, 0.1f)]   // Uncommon - green
    [InlineData(2, 0.1f, 0.5f, 1f)]   // Rare - blue
    [InlineData(3, 0.6f, 0.2f, 1f)]   // Epic - purple
    [InlineData(4, 1f, 0.5f, 0f)]     // Legendary - orange
    public void GetQualityColor_ValidQuality_ReturnsExpectedColor(int quality, float r, float g, float b)
    {
        var expected = new Color(r, g, b);
        var actual = ItemIconCatalog.GetQualityColor(quality);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetQualityColor_NegativeQuality_ReturnsCommonWhite()
    {
        var expected = new Color(1f, 1f, 1f);
        var actual = ItemIconCatalog.GetQualityColor(-1);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(100)]
    public void GetQualityColor_QualityGreaterThanFour_ReturnsCommonWhite(int quality)
    {
        var expected = new Color(1f, 1f, 1f);
        var actual = ItemIconCatalog.GetQualityColor(quality);
        Assert.Equal(expected, actual);
    }
}
