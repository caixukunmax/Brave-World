using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// Debug panel system tab. Build and config sync live in partial files.
    /// </summary>
    public partial class DebugPanelSystemTab : DebugPanelTab
    {
        private HSlider _moveCheckRatioSlider;
        private Label _moveCheckRatioValue;
        private HSlider _moveDualStartSlider;
        private Label _moveDualStartValue;
        private HSlider _moveDualEndSlider;
        private Label _moveDualEndValue;

        // 回弹动画配置
        private HSlider _bounceDurationSlider = null!;
        private Label _bounceDurationValue = null!;
        private HSlider _bounceOvershootRatioSlider = null!;
        private Label _bounceOvershootRatioValue = null!;
        private HSlider _bounceOvershootThresholdSlider = null!;
        private Label _bounceOvershootThresholdValue = null!;

        // 怪物死亡效果配置
        private OptionButton _deathEffectOption = null!;
        private HSlider _deathFadeDurationSlider = null!;
        private Label _deathFadeDurationValue = null!;
        private HSlider _deathGrayDelaySlider = null!;
        private Label _deathGrayDelayValue = null!;

        // 背包配置
        private HSlider _backpackPaddingHSlider = null!;
        private Label _backpackPaddingHValue = null!;
        private HSlider _backpackPaddingTopSlider = null!;
        private Label _backpackPaddingTopValue = null!;
        private HSlider _backpackContentWidthSlider = null!;
        private Label _backpackContentWidthValue = null!;
        private HSlider _backpackAreaHeightSlider = null!;
        private Label _backpackAreaHeightValue = null!;
        private HSlider _backpackItemSpacingSlider = null!;
        private Label _backpackItemSpacingValue = null!;
        private CheckButton _lockHorizontalResizeCheck = null!;
        private CheckButton _lockVerticalResizeCheck = null!;
        private CheckButton _backpackDebugBorderCheck = null!;
        private CheckButton _backpackShowDimensionsCheck = null!;
        private HSlider _backpackHoverCornerRadiusSlider = null!;
        private Label _backpackHoverCornerRadiusValue = null!;
        private HSlider _backpackHoverBorderWidthSlider = null!;
        private HSlider _backpackReorderDurationSlider = null!;
        private Label _backpackReorderDurationValue = null!;
        private Label _backpackHoverBorderWidthValue = null!;
        private ColorPickerButton _backpackHoverColorPicker = null!;

        public DebugPanelSystemTab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "system";

        public override void ConnectSignals()
        {
        }

        public override void DisconnectSignals()
        {
            if (_deathEffectOption != null) _deathEffectOption.ItemSelected -= OnDeathEffectModeChanged;
            if (_deathFadeDurationSlider != null) _deathFadeDurationSlider.ValueChanged -= OnDeathFadeDurationChanged;
            if (_deathGrayDelaySlider != null) _deathGrayDelaySlider.ValueChanged -= OnDeathGrayDelayChanged;
            if (_moveCheckRatioSlider != null) _moveCheckRatioSlider.ValueChanged -= OnMoveCheckRatioChanged;
            if (_moveDualStartSlider != null) _moveDualStartSlider.ValueChanged -= OnMoveDualStartChanged;
            if (_moveDualEndSlider != null) _moveDualEndSlider.ValueChanged -= OnMoveDualEndChanged;
            if (_bounceDurationSlider != null) _bounceDurationSlider.ValueChanged -= OnBounceDurationChanged;
            if (_bounceOvershootRatioSlider != null) _bounceOvershootRatioSlider.ValueChanged -= OnBounceOvershootRatioChanged;
            if (_bounceOvershootThresholdSlider != null) _bounceOvershootThresholdSlider.ValueChanged -= OnBounceOvershootThresholdChanged;

            if (_backpackPaddingHSlider != null) _backpackPaddingHSlider.ValueChanged -= OnBackpackPaddingHChanged;
            if (_backpackPaddingTopSlider != null) _backpackPaddingTopSlider.ValueChanged -= OnBackpackPaddingTopChanged;
            if (_backpackContentWidthSlider != null) _backpackContentWidthSlider.ValueChanged -= OnBackpackContentWidthChanged;
            if (_backpackAreaHeightSlider != null) _backpackAreaHeightSlider.ValueChanged -= OnBackpackAreaHeightChanged;
            if (_backpackItemSpacingSlider != null) _backpackItemSpacingSlider.ValueChanged -= OnBackpackItemSpacingChanged;
            if (_lockHorizontalResizeCheck != null) _lockHorizontalResizeCheck.Toggled -= OnLockHorizontalResizeToggled;
            if (_lockVerticalResizeCheck != null) _lockVerticalResizeCheck.Toggled -= OnLockVerticalResizeToggled;
            if (_backpackDebugBorderCheck != null) _backpackDebugBorderCheck.Toggled -= OnBackpackDebugBorderToggled;
            if (_backpackShowDimensionsCheck != null) _backpackShowDimensionsCheck.Toggled -= OnBackpackShowDimensionsToggled;
            if (_backpackHoverCornerRadiusSlider != null) _backpackHoverCornerRadiusSlider.ValueChanged -= OnBackpackHoverCornerRadiusChanged;
            if (_backpackHoverBorderWidthSlider != null) _backpackHoverBorderWidthSlider.ValueChanged -= OnBackpackHoverBorderWidthChanged;
            if (_backpackReorderDurationSlider != null) _backpackReorderDurationSlider.ValueChanged -= OnBackpackReorderDurationChanged;
            if (_backpackHoverColorPicker != null) _backpackHoverColorPicker.ColorChanged -= OnBackpackHoverColorChanged;
        }

        public (int checkRatio, int dualStart, int dualEnd) GetMoveSystemValues()
        {
            return (
                (int)_moveCheckRatioSlider.Value,
                (int)_moveDualStartSlider.Value,
                (int)_moveDualEndSlider.Value
            );
        }
    }
}
