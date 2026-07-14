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
            cfg.SetValue("system_tab", "backpack_reorder_duration", InventoryUI.BackpackReorderAnimationDuration);
            cfg.SetValue("system_tab", "backpack_hover_border_width", InventoryUI.BackpackHoverBoxBorderWidth);
            cfg.SetValue("system_tab", "backpack_hover_box_color", InventoryUI.BackpackHoverBoxColor);

            // 方向箭头配置
            cfg.SetValue("system_tab", "direction_arrow_style", EntityBase.DirectionArrowStyle);
            cfg.SetValue("system_tab", "direction_arrow_size", EntityBase.DirectionArrowSize);
            cfg.SetValue("system_tab", "direction_arrow_color", EntityBase.DirectionArrowColor);
            cfg.SetValue("system_tab", "direction_arrow_alpha", EntityBase.DirectionArrowAlpha);

            string[] dirKeys = { "right", "down", "left", "up" };
            for (int d = 0; d < 4; d++)
            {
                cfg.SetValue("system_tab", $"direction_arrow_offset_x_{dirKeys[d]}", EntityBase.DirectionArrowOffsets[d].X);
                cfg.SetValue("system_tab", $"direction_arrow_offset_y_{dirKeys[d]}", EntityBase.DirectionArrowOffsets[d].Y);
                cfg.SetValue("system_tab", $"direction_arrow_angle_{dirKeys[d]}", EntityBase.DirectionArrowAngles[d]);
            }

            // 剧情窗口配置
            cfg.SetValue("system_tab", "story_panel_width", StoryPanel.StoryPanelWidth);
            cfg.SetValue("system_tab", "story_panel_height", StoryPanel.StoryPanelHeight);
            cfg.SetValue("system_tab", "story_font_size", StoryPanel.StoryFontSize);
            cfg.SetValue("system_tab", "story_panel_alpha", StoryPanel.StoryPanelAlpha);
            cfg.SetValue("system_tab", "story_content_padding", StoryPanel.StoryContentPadding);
            cfg.SetValue("system_tab", "story_typewriter_enabled", StoryPanel.StoryTypewriterEnabled);
            cfg.SetValue("system_tab", "story_typewriter_interval_ms", StoryPanel.StoryTypewriterIntervalMs);
            cfg.SetValue("system_tab", "story_history_entry_spacing", StoryPanel.StoryHistoryEntrySpacing);
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
            InventoryUI.BackpackReorderAnimationDuration = (float)(double)cfg.GetValue("system_tab", "backpack_reorder_duration", 0.15);
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

            _backpackReorderDurationSlider.SetBlockSignals(true);
            _backpackReorderDurationSlider.Value = InventoryUI.BackpackReorderAnimationDuration;
            _backpackReorderDurationSlider.SetBlockSignals(false);
            _backpackReorderDurationValue.Text = InventoryUI.BackpackReorderAnimationDuration.ToString("F2");

            _backpackHoverBorderWidthSlider.SetBlockSignals(true);
            _backpackHoverBorderWidthSlider.Value = InventoryUI.BackpackHoverBoxBorderWidth;
            _backpackHoverBorderWidthSlider.SetBlockSignals(false);
            _backpackHoverBorderWidthValue.Text = InventoryUI.BackpackHoverBoxBorderWidth.ToString();

            _backpackHoverColorPicker.SetBlockSignals(true);
            _backpackHoverColorPicker.Color = InventoryUI.BackpackHoverBoxColor;
            _backpackHoverColorPicker.SetBlockSignals(false);

            // 方向箭头配置
            EntityBase.DirectionArrowStyle = (int)(double)cfg.GetValue("system_tab", "direction_arrow_style", 0.0);
            EntityBase.DirectionArrowSize = (float)(double)cfg.GetValue("system_tab", "direction_arrow_size", 0.35);
            EntityBase.DirectionArrowColor = (Color)cfg.GetValue("system_tab", "direction_arrow_color", new Color(1f, 0.9f, 0.2f, 0.9f));
            EntityBase.DirectionArrowAlpha = (float)(double)cfg.GetValue("system_tab", "direction_arrow_alpha", 0.9);
            Vector2[] defaultOffsets = { new(15, 0), new(0, 15), new(-15, 0), new(0, -15) };
            float[] defaultAngles = { 0f, 90f, 180f, 270f };
            string[] dirKeys = { "right", "down", "left", "up" };
            for (int d = 0; d < 4; d++)
            {
                float ox = (float)(double)cfg.GetValue("system_tab", $"direction_arrow_offset_x_{dirKeys[d]}", (double)defaultOffsets[d].X);
                float oy = (float)(double)cfg.GetValue("system_tab", $"direction_arrow_offset_y_{dirKeys[d]}", (double)defaultOffsets[d].Y);
                float ang = (float)(double)cfg.GetValue("system_tab", $"direction_arrow_angle_{dirKeys[d]}", (double)defaultAngles[d]);
                EntityBase.DirectionArrowOffsets[d] = new Vector2(ox, oy);
                EntityBase.DirectionArrowAngles[d] = ang;
            }

            _directionArrowStyleOption.SetBlockSignals(true);
            _directionArrowStyleOption.Select(EntityBase.DirectionArrowStyle);
            _directionArrowStyleOption.SetBlockSignals(false);

            _directionArrowSizeSlider.SetBlockSignals(true);
            _directionArrowSizeSlider.Value = EntityBase.DirectionArrowSize;
            _directionArrowSizeSlider.SetBlockSignals(false);
            _directionArrowSizeValue.Text = EntityBase.DirectionArrowSize.ToString("F2");

            _directionArrowColorPicker.SetBlockSignals(true);
            _directionArrowColorPicker.Color = EntityBase.DirectionArrowColor;
            _directionArrowColorPicker.SetBlockSignals(false);

            _directionArrowAlphaSlider.SetBlockSignals(true);
            _directionArrowAlphaSlider.Value = EntityBase.DirectionArrowAlpha;
            _directionArrowAlphaSlider.SetBlockSignals(false);
            _directionArrowAlphaValue.Text = EntityBase.DirectionArrowAlpha.ToString("F2");

            for (int d = 0; d < 4; d++)
            {
                _directionArrowOffsetXSliders[d].SetBlockSignals(true);
                _directionArrowOffsetXSliders[d].Value = EntityBase.DirectionArrowOffsets[d].X;
                _directionArrowOffsetXSliders[d].SetBlockSignals(false);
                _directionArrowOffsetXValues[d].Text = EntityBase.DirectionArrowOffsets[d].X.ToString("F0");

                _directionArrowOffsetYSliders[d].SetBlockSignals(true);
                _directionArrowOffsetYSliders[d].Value = EntityBase.DirectionArrowOffsets[d].Y;
                _directionArrowOffsetYSliders[d].SetBlockSignals(false);
                _directionArrowOffsetYValues[d].Text = EntityBase.DirectionArrowOffsets[d].Y.ToString("F0");

                _directionArrowAngleSliders[d].SetBlockSignals(true);
                _directionArrowAngleSliders[d].Value = EntityBase.DirectionArrowAngles[d];
                _directionArrowAngleSliders[d].SetBlockSignals(false);
                _directionArrowAngleValues[d].Text = EntityBase.DirectionArrowAngles[d].ToString("F0");
            }

            // 剧情窗口配置
            StoryPanel.StoryPanelWidth = (int)(double)cfg.GetValue("system_tab", "story_panel_width", 560.0);
            StoryPanel.StoryPanelHeight = (int)(double)cfg.GetValue("system_tab", "story_panel_height", 240.0);
            StoryPanel.StoryFontSize = (int)(double)cfg.GetValue("system_tab", "story_font_size", 18.0);
            StoryPanel.StoryPanelAlpha = (int)(double)cfg.GetValue("system_tab", "story_panel_alpha", 85.0);
            StoryPanel.StoryTypewriterEnabled = (bool)cfg.GetValue("system_tab", "story_typewriter_enabled", false);
            StoryPanel.StoryTypewriterIntervalMs = (int)(double)cfg.GetValue("system_tab", "story_typewriter_interval_ms", 50.0);
            StoryPanel.StoryHistoryEntrySpacing = (int)(double)cfg.GetValue("system_tab", "story_history_entry_spacing", 8.0);

            _storyPanelWidthSlider.SetBlockSignals(true);
            _storyPanelWidthSlider.Value = StoryPanel.StoryPanelWidth;
            _storyPanelWidthSlider.SetBlockSignals(false);
            _storyPanelWidthValue.Text = StoryPanel.StoryPanelWidth.ToString();

            _storyPanelHeightSlider.SetBlockSignals(true);
            _storyPanelHeightSlider.Value = StoryPanel.StoryPanelHeight;
            _storyPanelHeightSlider.SetBlockSignals(false);
            _storyPanelHeightValue.Text = StoryPanel.StoryPanelHeight.ToString();

            _storyFontSizeSlider.SetBlockSignals(true);
            _storyFontSizeSlider.Value = StoryPanel.StoryFontSize;
            _storyFontSizeSlider.SetBlockSignals(false);
            _storyFontSizeValue.Text = StoryPanel.StoryFontSize.ToString();

            _storyPanelAlphaSlider.SetBlockSignals(true);
            _storyPanelAlphaSlider.Value = StoryPanel.StoryPanelAlpha;
            _storyPanelAlphaSlider.SetBlockSignals(false);
            _storyPanelAlphaValue.Text = StoryPanel.StoryPanelAlpha.ToString();

            StoryPanel.StoryContentPadding = (int)(double)cfg.GetValue("system_tab", "story_content_padding", 16.0);

            _storyContentPaddingSlider.SetBlockSignals(true);
            _storyContentPaddingSlider.Value = StoryPanel.StoryContentPadding;
            _storyContentPaddingSlider.SetBlockSignals(false);
            _storyContentPaddingValue.Text = StoryPanel.StoryContentPadding.ToString();

            _storyTypewriterCheck.SetBlockSignals(true);
            _storyTypewriterCheck.ButtonPressed = StoryPanel.StoryTypewriterEnabled;
            _storyTypewriterCheck.SetBlockSignals(false);

            _storyTypewriterIntervalSlider.SetBlockSignals(true);
            _storyTypewriterIntervalSlider.Value = StoryPanel.StoryTypewriterIntervalMs;
            _storyTypewriterIntervalSlider.SetBlockSignals(false);
            _storyTypewriterIntervalValue.Text = StoryPanel.StoryTypewriterIntervalMs.ToString();

            _storyHistorySpacingSlider.SetBlockSignals(true);
            _storyHistorySpacingSlider.Value = StoryPanel.StoryHistoryEntrySpacing;
            _storyHistorySpacingSlider.SetBlockSignals(false);
            _storyHistorySpacingValue.Text = StoryPanel.StoryHistoryEntrySpacing.ToString();

            ApplyStoryPanelSettings();
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

            _backpackReorderDurationSlider.SetBlockSignals(true);
            _backpackReorderDurationSlider.Value = InventoryUI.BackpackReorderAnimationDuration;
            _backpackReorderDurationSlider.SetBlockSignals(false);
            _backpackReorderDurationValue.Text = InventoryUI.BackpackReorderAnimationDuration.ToString("F2");

            _backpackHoverBorderWidthSlider.SetBlockSignals(true);
            _backpackHoverBorderWidthSlider.Value = InventoryUI.BackpackHoverBoxBorderWidth;
            _backpackHoverBorderWidthSlider.SetBlockSignals(false);
            _backpackHoverBorderWidthValue.Text = InventoryUI.BackpackHoverBoxBorderWidth.ToString();

            _backpackHoverColorPicker.SetBlockSignals(true);
            _backpackHoverColorPicker.Color = InventoryUI.BackpackHoverBoxColor;
            _backpackHoverColorPicker.SetBlockSignals(false);

            // 方向箭头配置
            _directionArrowStyleOption.SetBlockSignals(true);
            _directionArrowStyleOption.Select(EntityBase.DirectionArrowStyle);
            _directionArrowStyleOption.SetBlockSignals(false);

            _directionArrowSizeSlider.SetBlockSignals(true);
            _directionArrowSizeSlider.Value = EntityBase.DirectionArrowSize;
            _directionArrowSizeSlider.SetBlockSignals(false);
            _directionArrowSizeValue.Text = EntityBase.DirectionArrowSize.ToString("F2");

            _directionArrowColorPicker.SetBlockSignals(true);
            _directionArrowColorPicker.Color = EntityBase.DirectionArrowColor;
            _directionArrowColorPicker.SetBlockSignals(false);

            _directionArrowAlphaSlider.SetBlockSignals(true);
            _directionArrowAlphaSlider.Value = EntityBase.DirectionArrowAlpha;
            _directionArrowAlphaSlider.SetBlockSignals(false);
            _directionArrowAlphaValue.Text = EntityBase.DirectionArrowAlpha.ToString("F2");

            for (int d = 0; d < 4; d++)
            {
                _directionArrowOffsetXSliders[d].SetBlockSignals(true);
                _directionArrowOffsetXSliders[d].Value = EntityBase.DirectionArrowOffsets[d].X;
                _directionArrowOffsetXSliders[d].SetBlockSignals(false);
                _directionArrowOffsetXValues[d].Text = EntityBase.DirectionArrowOffsets[d].X.ToString("F0");

                _directionArrowOffsetYSliders[d].SetBlockSignals(true);
                _directionArrowOffsetYSliders[d].Value = EntityBase.DirectionArrowOffsets[d].Y;
                _directionArrowOffsetYSliders[d].SetBlockSignals(false);
                _directionArrowOffsetYValues[d].Text = EntityBase.DirectionArrowOffsets[d].Y.ToString("F0");

                _directionArrowAngleSliders[d].SetBlockSignals(true);
                _directionArrowAngleSliders[d].Value = EntityBase.DirectionArrowAngles[d];
                _directionArrowAngleSliders[d].SetBlockSignals(false);
                _directionArrowAngleValues[d].Text = EntityBase.DirectionArrowAngles[d].ToString("F0");
            }

            // 剧情窗口配置
            _storyPanelWidthSlider.SetBlockSignals(true);
            _storyPanelWidthSlider.Value = StoryPanel.StoryPanelWidth;
            _storyPanelWidthSlider.SetBlockSignals(false);
            _storyPanelWidthValue.Text = StoryPanel.StoryPanelWidth.ToString();

            _storyPanelHeightSlider.SetBlockSignals(true);
            _storyPanelHeightSlider.Value = StoryPanel.StoryPanelHeight;
            _storyPanelHeightSlider.SetBlockSignals(false);
            _storyPanelHeightValue.Text = StoryPanel.StoryPanelHeight.ToString();

            _storyFontSizeSlider.SetBlockSignals(true);
            _storyFontSizeSlider.Value = StoryPanel.StoryFontSize;
            _storyFontSizeSlider.SetBlockSignals(false);
            _storyFontSizeValue.Text = StoryPanel.StoryFontSize.ToString();

            _storyPanelAlphaSlider.SetBlockSignals(true);
            _storyPanelAlphaSlider.Value = StoryPanel.StoryPanelAlpha;
            _storyPanelAlphaSlider.SetBlockSignals(false);
            _storyPanelAlphaValue.Text = StoryPanel.StoryPanelAlpha.ToString();

            _storyContentPaddingSlider.SetBlockSignals(true);
            _storyContentPaddingSlider.Value = StoryPanel.StoryContentPadding;
            _storyContentPaddingSlider.SetBlockSignals(false);
            _storyContentPaddingValue.Text = StoryPanel.StoryContentPadding.ToString();

            _storyTypewriterCheck.SetBlockSignals(true);
            _storyTypewriterCheck.ButtonPressed = StoryPanel.StoryTypewriterEnabled;
            _storyTypewriterCheck.SetBlockSignals(false);

            _storyTypewriterIntervalSlider.SetBlockSignals(true);
            _storyTypewriterIntervalSlider.Value = StoryPanel.StoryTypewriterIntervalMs;
            _storyTypewriterIntervalSlider.SetBlockSignals(false);
            _storyTypewriterIntervalValue.Text = StoryPanel.StoryTypewriterIntervalMs.ToString();

            _storyHistorySpacingSlider.SetBlockSignals(true);
            _storyHistorySpacingSlider.Value = StoryPanel.StoryHistoryEntrySpacing;
            _storyHistorySpacingSlider.SetBlockSignals(false);
            _storyHistorySpacingValue.Text = StoryPanel.StoryHistoryEntrySpacing.ToString();
        }
    }
}
