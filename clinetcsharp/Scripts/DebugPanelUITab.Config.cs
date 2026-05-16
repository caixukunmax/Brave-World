using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanelUITab
    {
        public override void SaveConfig(ConfigFile cfg)
        {
            cfg.SetValue("skill_bar", "icon_size", _skillBarIconSizeSlider?.Value ?? 44);
            cfg.SetValue("skill_bar", "spacing", _skillBarSpacingSlider?.Value ?? 6);
            cfg.SetValue("skill_bar", "name_font_size", _skillBarNameFontSizeSlider?.Value ?? 10);
            cfg.SetValue("skill_bar", "margin_right", _skillBarMarginRightSlider?.Value ?? 20);
            cfg.SetValue("skill_bar", "margin_bottom", _skillBarMarginBottomSlider?.Value ?? 20);

            cfg.SetValue("fn_bar", "offset_x", _fnBarOffsetXSlider?.Value ?? 8);
            cfg.SetValue("fn_bar", "offset_y", _fnBarOffsetYSlider?.Value ?? 8);
            cfg.SetValue("fn_bar", "spacing", _fnBarSpacingSlider?.Value ?? 3);

            cfg.SetValue("buff_bar", "icon_size", _buffBarIconSizeSlider?.Value ?? 36);
            cfg.SetValue("buff_bar", "spacing", _buffBarSpacingSlider?.Value ?? 4);
            cfg.SetValue("buff_bar", "offset_x", _buffBarOffsetXSlider?.Value ?? 0);
            cfg.SetValue("buff_bar", "offset_y", _buffBarOffsetYSlider?.Value ?? 0);
            cfg.SetValue("buff_bar", "right_align", _buffBarRightAlignCheck?.ButtonPressed ?? false);
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded)
                return;

            LoadSkillBarConfig(cfg);
            ApplySkillBarSettings();

            LoadFunctionBarConfig(cfg);
            ApplyFnBarSettings();

            LoadBuffBarConfig(cfg);
            ApplyBuffBarSettings();
        }

        private void LoadSkillBarConfig(ConfigFile cfg)
        {
            if (_skillBarIconSizeSlider != null)
            {
                _skillBarIconSizeSlider.SetBlockSignals(true);
                _skillBarIconSizeSlider.Value = (double)cfg.GetValue("skill_bar", "icon_size", 44.0);
                _skillBarIconSizeSlider.SetBlockSignals(false);
            }

            if (_skillBarSpacingSlider != null)
            {
                _skillBarSpacingSlider.SetBlockSignals(true);
                _skillBarSpacingSlider.Value = (double)cfg.GetValue("skill_bar", "spacing", 6.0);
                _skillBarSpacingSlider.SetBlockSignals(false);
            }

            if (_skillBarNameFontSizeSlider != null)
            {
                _skillBarNameFontSizeSlider.SetBlockSignals(true);
                _skillBarNameFontSizeSlider.Value = (double)cfg.GetValue("skill_bar", "name_font_size", 10.0);
                _skillBarNameFontSizeSlider.SetBlockSignals(false);
            }

            if (_skillBarMarginRightSlider != null)
            {
                _skillBarMarginRightSlider.SetBlockSignals(true);
                _skillBarMarginRightSlider.Value = (double)cfg.GetValue("skill_bar", "margin_right", 20.0);
                _skillBarMarginRightSlider.SetBlockSignals(false);
            }

            if (_skillBarMarginBottomSlider != null)
            {
                _skillBarMarginBottomSlider.SetBlockSignals(true);
                _skillBarMarginBottomSlider.Value = (double)cfg.GetValue("skill_bar", "margin_bottom", 20.0);
                _skillBarMarginBottomSlider.SetBlockSignals(false);
            }
        }

        private void LoadFunctionBarConfig(ConfigFile cfg)
        {
            if (_fnBarOffsetXSlider != null)
            {
                _fnBarOffsetXSlider.SetBlockSignals(true);
                _fnBarOffsetXSlider.Value = (double)cfg.GetValue("fn_bar", "offset_x", 8.0);
                _fnBarOffsetXSlider.SetBlockSignals(false);
            }

            if (_fnBarOffsetYSlider != null)
            {
                _fnBarOffsetYSlider.SetBlockSignals(true);
                _fnBarOffsetYSlider.Value = (double)cfg.GetValue("fn_bar", "offset_y", 8.0);
                _fnBarOffsetYSlider.SetBlockSignals(false);
            }

            if (_fnBarSpacingSlider != null)
            {
                _fnBarSpacingSlider.SetBlockSignals(true);
                _fnBarSpacingSlider.Value = (double)cfg.GetValue("fn_bar", "spacing", 3.0);
                _fnBarSpacingSlider.SetBlockSignals(false);
            }
        }

        private void LoadBuffBarConfig(ConfigFile cfg)
        {
            if (_buffBarIconSizeSlider != null)
            {
                _buffBarIconSizeSlider.SetBlockSignals(true);
                _buffBarIconSizeSlider.Value = (double)cfg.GetValue("buff_bar", "icon_size", 36.0);
                _buffBarIconSizeSlider.SetBlockSignals(false);
            }

            if (_buffBarSpacingSlider != null)
            {
                _buffBarSpacingSlider.SetBlockSignals(true);
                _buffBarSpacingSlider.Value = (double)cfg.GetValue("buff_bar", "spacing", 4.0);
                _buffBarSpacingSlider.SetBlockSignals(false);
            }

            if (_buffBarOffsetXSlider != null)
            {
                _buffBarOffsetXSlider.SetBlockSignals(true);
                _buffBarOffsetXSlider.Value = (double)cfg.GetValue("buff_bar", "offset_x", 0.0);
                _buffBarOffsetXSlider.SetBlockSignals(false);
            }

            if (_buffBarOffsetYSlider != null)
            {
                _buffBarOffsetYSlider.SetBlockSignals(true);
                _buffBarOffsetYSlider.Value = (double)cfg.GetValue("buff_bar", "offset_y", 0.0);
                _buffBarOffsetYSlider.SetBlockSignals(false);
            }

            if (_buffBarRightAlignCheck != null)
            {
                _buffBarRightAlignCheck.SetBlockSignals(true);
                _buffBarRightAlignCheck.ButtonPressed = (bool)cfg.GetValue("buff_bar", "right_align", false);
                _buffBarRightAlignCheck.SetBlockSignals(false);
            }
        }

        public override void SyncToCurrentValues()
        {
            SyncSkillBarValues();
            SyncFunctionBarValues();
            SyncBuffBarValues();
        }

        private void SyncSkillBarValues()
        {
            var skillBar = Owner.GetTree()?.GetFirstNodeInGroup("skill_bar") as SkillBar;
            if (skillBar == null)
                return;

            if (_skillBarIconSizeSlider != null)
            {
                _skillBarIconSizeSlider.SetBlockSignals(true);
                _skillBarIconSizeSlider.Value = skillBar.IconSize;
                _skillBarIconSizeSlider.SetBlockSignals(false);
            }

            if (_skillBarSpacingSlider != null)
            {
                _skillBarSpacingSlider.SetBlockSignals(true);
                _skillBarSpacingSlider.Value = skillBar.SlotSpacing;
                _skillBarSpacingSlider.SetBlockSignals(false);
            }

            if (_skillBarNameFontSizeSlider != null)
            {
                _skillBarNameFontSizeSlider.SetBlockSignals(true);
                _skillBarNameFontSizeSlider.Value = skillBar.NameFontSize;
                _skillBarNameFontSizeSlider.SetBlockSignals(false);
            }

            if (_skillBarMarginRightSlider != null)
            {
                _skillBarMarginRightSlider.SetBlockSignals(true);
                _skillBarMarginRightSlider.Value = skillBar.MarginRight;
                _skillBarMarginRightSlider.SetBlockSignals(false);
            }

            if (_skillBarMarginBottomSlider != null)
            {
                _skillBarMarginBottomSlider.SetBlockSignals(true);
                _skillBarMarginBottomSlider.Value = skillBar.MarginBottom;
                _skillBarMarginBottomSlider.SetBlockSignals(false);
            }
        }

        private void SyncFunctionBarValues()
        {
            var functionBar = Owner.GetTree()?.GetFirstNodeInGroup("function_bar") as FunctionButtonBar;
            if (functionBar == null)
                return;

            if (_fnBarOffsetXSlider != null)
            {
                _fnBarOffsetXSlider.SetBlockSignals(true);
                _fnBarOffsetXSlider.Value = functionBar.OffsetX;
                _fnBarOffsetXSlider.SetBlockSignals(false);
            }

            if (_fnBarOffsetYSlider != null)
            {
                _fnBarOffsetYSlider.SetBlockSignals(true);
                _fnBarOffsetYSlider.Value = functionBar.OffsetY;
                _fnBarOffsetYSlider.SetBlockSignals(false);
            }

            if (_fnBarSpacingSlider != null)
            {
                _fnBarSpacingSlider.SetBlockSignals(true);
                _fnBarSpacingSlider.Value = functionBar.ButtonSpacing;
                _fnBarSpacingSlider.SetBlockSignals(false);
            }
        }

        private void SyncBuffBarValues()
        {
            var buffBar = Owner.GetTree()?.GetFirstNodeInGroup("buff_bar") as BuffBar;
            if (buffBar == null)
                return;

            if (_buffBarIconSizeSlider != null)
            {
                _buffBarIconSizeSlider.SetBlockSignals(true);
                _buffBarIconSizeSlider.Value = buffBar.IconSize;
                _buffBarIconSizeSlider.SetBlockSignals(false);
            }

            if (_buffBarSpacingSlider != null)
            {
                _buffBarSpacingSlider.SetBlockSignals(true);
                _buffBarSpacingSlider.Value = buffBar.SlotSpacing;
                _buffBarSpacingSlider.SetBlockSignals(false);
            }

            if (_buffBarOffsetXSlider != null)
            {
                _buffBarOffsetXSlider.SetBlockSignals(true);
                _buffBarOffsetXSlider.Value = buffBar.OffsetX;
                _buffBarOffsetXSlider.SetBlockSignals(false);
            }

            if (_buffBarOffsetYSlider != null)
            {
                _buffBarOffsetYSlider.SetBlockSignals(true);
                _buffBarOffsetYSlider.Value = buffBar.OffsetY;
                _buffBarOffsetYSlider.SetBlockSignals(false);
            }

            if (_buffBarRightAlignCheck != null)
            {
                _buffBarRightAlignCheck.SetBlockSignals(true);
                _buffBarRightAlignCheck.ButtonPressed = buffBar.RightAlign;
                _buffBarRightAlignCheck.SetBlockSignals(false);
            }
        }
    }
}
