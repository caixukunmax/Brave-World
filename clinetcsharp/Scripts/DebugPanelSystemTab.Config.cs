using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanelSystemTab
    {
        public override void SaveConfig(ConfigFile cfg)
        {
            var mm = MonsterManager;
            if (mm != null)
            {
                cfg.SetValue("system_tab", "death_effect_mode", mm.DeathEffectMode);
                cfg.SetValue("system_tab", "death_fade_duration", mm.DeathFadeDuration);
                cfg.SetValue("system_tab", "death_gray_delay", mm.DeathGrayDelay);
            }

            cfg.SetValue("system_tab", "bounce_duration", EntityBase.BounceBackDuration);
            cfg.SetValue("system_tab", "bounce_overshoot_ratio", EntityBase.BounceBackOvershootRatio);
            cfg.SetValue("system_tab", "bounce_overshoot_threshold", EntityBase.BounceBackOvershootThreshold);

            cfg.SetValue("system_tab", "backpack_padding_h", InventoryUI.BackpackPaddingH);
            cfg.SetValue("system_tab", "backpack_padding_top", InventoryUI.BackpackPaddingTop);
            cfg.SetValue("system_tab", "backpack_content_width", InventoryUI.BackpackContentWidth);
            cfg.SetValue("system_tab", "backpack_area_height", InventoryUI.BackpackAreaHeight);
            cfg.SetValue("system_tab", "backpack_item_spacing", InventoryUI.BackpackItemSpacing);
            cfg.SetValue("system_tab", "backpack_lock_horizontal_resize", InventoryUI.LockHorizontalResize);
            cfg.SetValue("system_tab", "backpack_lock_vertical_resize", InventoryUI.LockVerticalResize);
            cfg.SetValue("system_tab", "backpack_debug_border", InventoryUI.DebugDrawItemBorder);
            cfg.SetValue("system_tab", "backpack_show_item_dimensions", InventoryUI.DebugShowItemDimensions);
            cfg.SetValue("system_tab", "backpack_hover_corner_radius", InventoryUI.BackpackHoverCornerRadius);
            cfg.SetValue("system_tab", "backpack_hover_border_width", InventoryUI.BackpackHoverBoxBorderWidth);
            cfg.SetValue("system_tab", "backpack_hover_box_color", InventoryUI.BackpackHoverBoxColor);
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded) return;

            var mm = MonsterManager;
            if (mm != null)
            {
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
            }

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

            // 背包配置
            InventoryUI.BackpackPaddingH = (int)(double)cfg.GetValue("system_tab", "backpack_padding_h", 8.0);
            InventoryUI.BackpackPaddingTop = (int)(double)cfg.GetValue("system_tab", "backpack_padding_top", 0.0);
            InventoryUI.BackpackContentWidth = (int)(double)cfg.GetValue("system_tab", "backpack_content_width", 480.0);
            InventoryUI.BackpackAreaHeight = (int)(double)cfg.GetValue("system_tab", "backpack_area_height", 240.0);
            InventoryUI.BackpackItemSpacing = (int)(double)cfg.GetValue("system_tab", "backpack_item_spacing", 8.0);
            InventoryUI.LockHorizontalResize = (bool)cfg.GetValue("system_tab", "backpack_lock_horizontal_resize", true);
            InventoryUI.LockVerticalResize = (bool)cfg.GetValue("system_tab", "backpack_lock_vertical_resize", false);
            InventoryUI.DebugDrawItemBorder = (bool)cfg.GetValue("system_tab", "backpack_debug_border", true);
            InventoryUI.DebugShowItemDimensions = (bool)cfg.GetValue("system_tab", "backpack_show_item_dimensions", false);
            InventoryUI.BackpackHoverCornerRadius = (int)(double)cfg.GetValue("system_tab", "backpack_hover_corner_radius", 8.0);
            InventoryUI.BackpackHoverBoxBorderWidth = (int)(double)cfg.GetValue("system_tab", "backpack_hover_border_width", 2.0);
            InventoryUI.BackpackHoverBoxColor = (Color)cfg.GetValue("system_tab", "backpack_hover_box_color", Colors.White);

            _backpackPaddingHSlider.SetBlockSignals(true);
            _backpackPaddingHSlider.Value = InventoryUI.BackpackPaddingH;
            _backpackPaddingHSlider.SetBlockSignals(false);
            _backpackPaddingHValue.Text = InventoryUI.BackpackPaddingH.ToString();

            _backpackPaddingTopSlider.SetBlockSignals(true);
            _backpackPaddingTopSlider.Value = InventoryUI.BackpackPaddingTop;
            _backpackPaddingTopSlider.SetBlockSignals(false);
            _backpackPaddingTopValue.Text = InventoryUI.BackpackPaddingTop.ToString();

            _backpackContentWidthSlider.SetBlockSignals(true);
            _backpackContentWidthSlider.Value = InventoryUI.BackpackContentWidth;
            _backpackContentWidthSlider.SetBlockSignals(false);
            _backpackContentWidthValue.Text = InventoryUI.BackpackContentWidth.ToString();

            _backpackAreaHeightSlider.SetBlockSignals(true);
            _backpackAreaHeightSlider.Value = InventoryUI.BackpackAreaHeight;
            _backpackAreaHeightSlider.SetBlockSignals(false);
            _backpackAreaHeightValue.Text = InventoryUI.BackpackAreaHeight.ToString();

            _backpackItemSpacingSlider.SetBlockSignals(true);
            _backpackItemSpacingSlider.Value = InventoryUI.BackpackItemSpacing;
            _backpackItemSpacingSlider.SetBlockSignals(false);
            _backpackItemSpacingValue.Text = InventoryUI.BackpackItemSpacing.ToString();

            _lockHorizontalResizeCheck.SetBlockSignals(true);
            _lockHorizontalResizeCheck.ButtonPressed = InventoryUI.LockHorizontalResize;
            _lockHorizontalResizeCheck.SetBlockSignals(false);

            _lockVerticalResizeCheck.SetBlockSignals(true);
            _lockVerticalResizeCheck.ButtonPressed = InventoryUI.LockVerticalResize;
            _lockVerticalResizeCheck.SetBlockSignals(false);

            _backpackDebugBorderCheck.SetBlockSignals(true);
            _backpackDebugBorderCheck.ButtonPressed = InventoryUI.DebugDrawItemBorder;
            _backpackDebugBorderCheck.SetBlockSignals(false);

            _backpackShowDimensionsCheck.SetBlockSignals(true);
            _backpackShowDimensionsCheck.ButtonPressed = InventoryUI.DebugShowItemDimensions;
            _backpackShowDimensionsCheck.SetBlockSignals(false);

            _backpackHoverCornerRadiusSlider.SetBlockSignals(true);
            _backpackHoverCornerRadiusSlider.Value = InventoryUI.BackpackHoverCornerRadius;
            _backpackHoverCornerRadiusSlider.SetBlockSignals(false);
            _backpackHoverCornerRadiusValue.Text = InventoryUI.BackpackHoverCornerRadius.ToString();

            _backpackHoverBorderWidthSlider.SetBlockSignals(true);
            _backpackHoverBorderWidthSlider.Value = InventoryUI.BackpackHoverBoxBorderWidth;
            _backpackHoverBorderWidthSlider.SetBlockSignals(false);
            _backpackHoverBorderWidthValue.Text = InventoryUI.BackpackHoverBoxBorderWidth.ToString();

            _backpackHoverColorPicker.SetBlockSignals(true);
            _backpackHoverColorPicker.Color = InventoryUI.BackpackHoverBoxColor;
            _backpackHoverColorPicker.SetBlockSignals(false);

            ApplyInventorySettings();
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

            // 背包配置
            _backpackPaddingHSlider.SetBlockSignals(true);
            _backpackPaddingHSlider.Value = InventoryUI.BackpackPaddingH;
            _backpackPaddingHSlider.SetBlockSignals(false);
            _backpackPaddingHValue.Text = InventoryUI.BackpackPaddingH.ToString();

            _backpackPaddingTopSlider.SetBlockSignals(true);
            _backpackPaddingTopSlider.Value = InventoryUI.BackpackPaddingTop;
            _backpackPaddingTopSlider.SetBlockSignals(false);
            _backpackPaddingTopValue.Text = InventoryUI.BackpackPaddingTop.ToString();

            _backpackContentWidthSlider.SetBlockSignals(true);
            _backpackContentWidthSlider.Value = InventoryUI.BackpackContentWidth;
            _backpackContentWidthSlider.SetBlockSignals(false);
            _backpackContentWidthValue.Text = InventoryUI.BackpackContentWidth.ToString();

            _backpackAreaHeightSlider.SetBlockSignals(true);
            _backpackAreaHeightSlider.Value = InventoryUI.BackpackAreaHeight;
            _backpackAreaHeightSlider.SetBlockSignals(false);
            _backpackAreaHeightValue.Text = InventoryUI.BackpackAreaHeight.ToString();

            _backpackItemSpacingSlider.SetBlockSignals(true);
            _backpackItemSpacingSlider.Value = InventoryUI.BackpackItemSpacing;
            _backpackItemSpacingSlider.SetBlockSignals(false);
            _backpackItemSpacingValue.Text = InventoryUI.BackpackItemSpacing.ToString();

            _lockHorizontalResizeCheck.SetBlockSignals(true);
            _lockHorizontalResizeCheck.ButtonPressed = InventoryUI.LockHorizontalResize;
            _lockHorizontalResizeCheck.SetBlockSignals(false);

            _lockVerticalResizeCheck.SetBlockSignals(true);
            _lockVerticalResizeCheck.ButtonPressed = InventoryUI.LockVerticalResize;
            _lockVerticalResizeCheck.SetBlockSignals(false);

            _backpackDebugBorderCheck.SetBlockSignals(true);
            _backpackDebugBorderCheck.ButtonPressed = InventoryUI.DebugDrawItemBorder;
            _backpackDebugBorderCheck.SetBlockSignals(false);

            _backpackShowDimensionsCheck.SetBlockSignals(true);
            _backpackShowDimensionsCheck.ButtonPressed = InventoryUI.DebugShowItemDimensions;
            _backpackShowDimensionsCheck.SetBlockSignals(false);

            _backpackHoverCornerRadiusSlider.SetBlockSignals(true);
            _backpackHoverCornerRadiusSlider.Value = InventoryUI.BackpackHoverCornerRadius;
            _backpackHoverCornerRadiusSlider.SetBlockSignals(false);
            _backpackHoverCornerRadiusValue.Text = InventoryUI.BackpackHoverCornerRadius.ToString();

            _backpackHoverBorderWidthSlider.SetBlockSignals(true);
            _backpackHoverBorderWidthSlider.Value = InventoryUI.BackpackHoverBoxBorderWidth;
            _backpackHoverBorderWidthSlider.SetBlockSignals(false);
            _backpackHoverBorderWidthValue.Text = InventoryUI.BackpackHoverBoxBorderWidth.ToString();

            _backpackHoverColorPicker.SetBlockSignals(true);
            _backpackHoverColorPicker.Color = InventoryUI.BackpackHoverBoxColor;
            _backpackHoverColorPicker.SetBlockSignals(false);
        }
    }
}
