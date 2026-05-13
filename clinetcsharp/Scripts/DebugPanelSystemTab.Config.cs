using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanelSystemTab
    {
        public override void SaveConfig(ConfigFile cfg)
        {
            var mm = MonsterManager;
            if (mm == null) return;

            cfg.SetValue("system_tab", "death_effect_mode", mm.DeathEffectMode);
            cfg.SetValue("system_tab", "death_fade_duration", mm.DeathFadeDuration);
            cfg.SetValue("system_tab", "death_gray_delay", mm.DeathGrayDelay);

            cfg.SetValue("system_tab", "bounce_duration", EntityBase.BounceBackDuration);
            cfg.SetValue("system_tab", "bounce_overshoot_ratio", EntityBase.BounceBackOvershootRatio);
            cfg.SetValue("system_tab", "bounce_overshoot_threshold", EntityBase.BounceBackOvershootThreshold);
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded) return;

            var mm = MonsterManager;
            if (mm == null) return;

            mm.DeathEffectMode = (int)(double)cfg.GetValue("system_tab", "death_effect_mode", 1.0);
            mm.DeathFadeDuration = (float)(double)cfg.GetValue("system_tab", "death_fade_duration", 0.5);
            mm.DeathGrayDelay = (float)(double)cfg.GetValue("system_tab", "death_gray_delay", 3.0);

            _deathEffectOption.SetBlockSignals(true);
            _deathEffectOption.Select(mm.DeathEffectMode);
            _deathEffectOption.SetBlockSignals(false);

            _deathFadeDurationSlider.SetBlockSignals(true);
            _deathFadeDurationSlider.Value = mm.DeathFadeDuration;
            _deathFadeDurationSlider.SetBlockSignals(false);
            _deathFadeDurationValue.Text = mm.DeathFadeDuration.ToString("F1");

            _deathGrayDelaySlider.SetBlockSignals(true);
            _deathGrayDelaySlider.Value = mm.DeathGrayDelay;
            _deathGrayDelaySlider.SetBlockSignals(false);
            _deathGrayDelayValue.Text = mm.DeathGrayDelay.ToString("F1");

            // 回弹动画配置
            EntityBase.BounceBackDuration = (float)(double)cfg.GetValue("system_tab", "bounce_duration", 0.06);
            EntityBase.BounceBackOvershootRatio = (float)(double)cfg.GetValue("system_tab", "bounce_overshoot_ratio", 0.08);
            EntityBase.BounceBackOvershootThreshold = (float)(double)cfg.GetValue("system_tab", "bounce_overshoot_threshold", 0.40);

            _bounceDurationSlider.SetBlockSignals(true);
            _bounceDurationSlider.Value = EntityBase.BounceBackDuration;
            _bounceDurationSlider.SetBlockSignals(false);
            _bounceDurationValue.Text = EntityBase.BounceBackDuration.ToString("F2");

            _bounceOvershootRatioSlider.SetBlockSignals(true);
            _bounceOvershootRatioSlider.Value = EntityBase.BounceBackOvershootRatio * 100f;
            _bounceOvershootRatioSlider.SetBlockSignals(false);
            _bounceOvershootRatioValue.Text = (EntityBase.BounceBackOvershootRatio * 100f).ToString("F0");

            _bounceOvershootThresholdSlider.SetBlockSignals(true);
            _bounceOvershootThresholdSlider.Value = EntityBase.BounceBackOvershootThreshold * 100f;
            _bounceOvershootThresholdSlider.SetBlockSignals(false);
            _bounceOvershootThresholdValue.Text = (EntityBase.BounceBackOvershootThreshold * 100f).ToString("F0");
        }

        public override void SyncToCurrentValues()
        {
            var configManager = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            if (configManager == null)
                return;

            var moveSystem = configManager.GetMoveSystem();
            if (moveSystem == null)
                return;

            _moveCheckRatioSlider.SetBlockSignals(true);
            _moveCheckRatioSlider.Value = moveSystem.CheckRatio;
            _moveCheckRatioSlider.SetBlockSignals(false);
            _moveCheckRatioValue.Text = moveSystem.CheckRatio.ToString();

            _moveDualStartSlider.SetBlockSignals(true);
            _moveDualStartSlider.Value = moveSystem.DualGridStartRatio;
            _moveDualStartSlider.SetBlockSignals(false);
            _moveDualStartValue.Text = moveSystem.DualGridStartRatio.ToString();

            _moveDualEndSlider.SetBlockSignals(true);
            _moveDualEndSlider.Value = moveSystem.DualGridEndRatio;
            _moveDualEndSlider.SetBlockSignals(false);
            _moveDualEndValue.Text = moveSystem.DualGridEndRatio.ToString();

            // 回弹动画配置
            _bounceDurationSlider.SetBlockSignals(true);
            _bounceDurationSlider.Value = EntityBase.BounceBackDuration;
            _bounceDurationSlider.SetBlockSignals(false);
            _bounceDurationValue.Text = EntityBase.BounceBackDuration.ToString("F2");

            _bounceOvershootRatioSlider.SetBlockSignals(true);
            _bounceOvershootRatioSlider.Value = EntityBase.BounceBackOvershootRatio * 100f;
            _bounceOvershootRatioSlider.SetBlockSignals(false);
            _bounceOvershootRatioValue.Text = (EntityBase.BounceBackOvershootRatio * 100f).ToString("F0");

            _bounceOvershootThresholdSlider.SetBlockSignals(true);
            _bounceOvershootThresholdSlider.Value = EntityBase.BounceBackOvershootThreshold * 100f;
            _bounceOvershootThresholdSlider.SetBlockSignals(false);
            _bounceOvershootThresholdValue.Text = (EntityBase.BounceBackOvershootThreshold * 100f).ToString("F0");
        }
    }
}
