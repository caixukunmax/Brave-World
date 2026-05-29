using Xunit;

namespace ClinetCSharp.Tests;

public class GridOverlayAntiAliasSoftnessPolicyTests
{
    [Fact]
    public void Clamp_WhenValueBelowMinimum_ReturnsMinimum()
    {
        Assert.Equal(
            GridOverlayAntiAliasSoftnessPolicy.Min,
            GridOverlayAntiAliasSoftnessPolicy.Clamp(GridOverlayAntiAliasSoftnessPolicy.Min - 1.0f));
    }

    [Fact]
    public void Clamp_WhenValueAboveMaximum_ReturnsMaximum()
    {
        Assert.Equal(
            GridOverlayAntiAliasSoftnessPolicy.Max,
            GridOverlayAntiAliasSoftnessPolicy.Clamp(GridOverlayAntiAliasSoftnessPolicy.Max + 1.0f));
    }

    [Fact]
    public void Clamp_WhenValueInRange_ReturnsOriginalValue()
    {
        Assert.Equal(1.6f, GridOverlayAntiAliasSoftnessPolicy.Clamp(1.6f));
    }

    [Fact]
    public void DefaultValue_StaysWithinSupportedRange()
    {
        Assert.InRange(
            GridOverlayAntiAliasSoftnessPolicy.Default,
            GridOverlayAntiAliasSoftnessPolicy.Min,
            GridOverlayAntiAliasSoftnessPolicy.Max);
    }
}