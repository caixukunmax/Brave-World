using Xunit;

namespace ClinetCSharp.Tests;

public class RoleControlCenterXResolverTests
{
    [Fact]
    public void ResolveX_WhenCenterXEnabled_ReturnsCenterAnchor()
    {
        var resolved = RoleControlCenterXResolver.ResolveX(manualX: 24f, centerX: true);

        Assert.Equal(0f, resolved);
    }

    [Fact]
    public void ResolveX_WhenCenterXDisabled_ReturnsManualX()
    {
        var resolved = RoleControlCenterXResolver.ResolveX(manualX: 24f, centerX: false);

        Assert.Equal(24f, resolved);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IsManualXEditable_MatchesCenterXState(bool centerX, bool expected)
    {
        Assert.Equal(expected, RoleControlCenterXResolver.IsManualXEditable(centerX));
    }

    [Fact]
    public void ResolveInitialCenterX_WhenPersistedValueExists_UsesPersistedValue()
    {
        var resolved = RoleControlCenterXResolver.ResolveInitialCenterX(persistedCenterX: true, offsetX: 48f);

        Assert.True(resolved);
    }

    [Fact]
    public void ResolveInitialCenterX_WhenPersistedValueMissingAndOffsetIsZero_DefaultsToCentered()
    {
        var resolved = RoleControlCenterXResolver.ResolveInitialCenterX(persistedCenterX: null, offsetX: 0f);

        Assert.True(resolved);
    }

    [Fact]
    public void ResolveInitialCenterX_WhenPersistedValueMissingAndOffsetIsNonZero_DefaultsToManualOffset()
    {
        var resolved = RoleControlCenterXResolver.ResolveInitialCenterX(persistedCenterX: null, offsetX: -35f);

        Assert.False(resolved);
    }

    [Theory]
    [InlineData("HSlider", true)]
    [InlineData("VSlider", true)]
    [InlineData("HScrollBar", true)]
    [InlineData("VScrollBar", true)]
    [InlineData("LineEdit", false)]
    [InlineData("CheckBox", false)]
    public void DebugPanelTransientFocusPolicy_ReleaseOnMouseRelease_OnlyForTransientDragControls(string className, bool expected)
    {
        Assert.Equal(expected, DebugPanelTransientFocusPolicy.ShouldReleaseOnLeftMouseRelease(className));
    }

    [Theory]
    [InlineData("HSlider", true)]
    [InlineData("VSlider", true)]
    [InlineData("HScrollBar", true)]
    [InlineData("VScrollBar", true)]
    [InlineData("Button", false)]
    [InlineData("LineEdit", false)]
    public void DebugPanelTransientFocusPolicy_DisableFocusMode_OnlyForTransientDragControls(string className, bool expected)
    {
        Assert.Equal(expected, DebugPanelTransientFocusPolicy.ShouldDisableFocusMode(className));
    }

    [Theory]
    [InlineData("HSlider", false)]
    [InlineData("VSlider", false)]
    [InlineData("HScrollBar", false)]
    [InlineData("VScrollBar", false)]
    [InlineData("PanelContainer", true)]
    [InlineData("VBoxContainer", true)]
    public void DebugPanelTransientFocusPolicy_StartPanelResize_DisabledForTransientDragControls(string className, bool expected)
    {
        Assert.Equal(expected, DebugPanelTransientFocusPolicy.ShouldStartPanelResize(className));
    }

    [Theory]
    [InlineData(DebugPanelEntityType.Player, DebugPanelEntityType.Monster, false)]
    [InlineData(DebugPanelEntityType.Monster, DebugPanelEntityType.Player, false)]
    [InlineData(DebugPanelEntityType.Npc, DebugPanelEntityType.Player, false)]
    [InlineData(DebugPanelEntityType.Player, DebugPanelEntityType.Player, true)]
    public void DebugPanelEntityStyleSyncPolicy_DoesNotShareStyleDataBetweenEntityTypes(
        DebugPanelEntityType source,
        DebugPanelEntityType target,
        bool expected)
    {
        Assert.Equal(expected, DebugPanelEntityStyleSyncPolicy.ShouldPropagateStyleChange(source, target));
    }

    [Theory]
    [InlineData(0.92, 0.05, "0.92")]
    [InlineData(0.5, 0.1, "0.5")]
    [InlineData(42.0, 1.0, "42")]
    [InlineData(800.0, 50.0, "800")]
    public void DebugPanelSliderValueFormatter_UsesStepPrecision(double value, double step, string expected)
    {
        Assert.Equal(expected, DebugPanelSliderValueFormatter.Format(value, step));
    }

    [Fact]
    public void DebugPanelLengthScalePolicy_UsesMillistepPrecision()
    {
        Assert.Equal(0.001, DebugPanelLengthScalePolicy.Step);
        Assert.Equal("0.721", DebugPanelLengthScalePolicy.Format(0.721));
    }

    [Theory]
    [InlineData("length", false)]
    [InlineData("hp_bar_length", false)]
    [InlineData("mp_bar_length", false)]
    [InlineData("castbar_length", false)]
    [InlineData("length_scale", true)]
    [InlineData("hp_bar_length_scale", true)]
    [InlineData("mp_bar_length_scale", true)]
    public void DebugPanelLengthScalePolicy_PersistsOnlyScaleKeys(string key, bool expected)
    {
        Assert.Equal(expected, DebugPanelLengthScalePolicy.ShouldPersistLengthKey(key));
        Assert.Equal(!expected, DebugPanelLengthScalePolicy.ShouldRemoveDirectLengthKey(key));
    }

    [Theory]
    [InlineData("HScrollBar", true, false)]
    [InlineData("HScrollBar", false, true)]
    [InlineData("HSlider", false, true)]
    [InlineData("LineEdit", false, false)]
    public void DebugPanelTransientFocusPolicy_ReleaseOnMousePress_WhenClickMovesAwayFromTransientControl(
        string className,
        bool pointerStillOnFocusOwner,
        bool expected)
    {
        Assert.Equal(
            expected,
            DebugPanelTransientFocusPolicy.ShouldReleaseOnLeftMousePress(className, pointerStillOnFocusOwner));
    }
}