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
