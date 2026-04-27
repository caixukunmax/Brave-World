using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel UI Tab — 技能栏配置
    /// 从 DebugPanel.DynamicUI.cs / DebugPanel.Handlers.cs / DebugPanel.cs 提取
    /// </summary>
    public class DebugPanelUITab : DebugPanelTab
    {
        #region Fields - SkillBar Controls
        private HSlider _skillBarIconSizeSlider;
        private Label _skillBarIconSizeValue;
        private HSlider _skillBarSpacingSlider;
        private Label _skillBarSpacingValue;
        private HSlider _skillBarMarginRightSlider;
        private Label _skillBarMarginRightValue;
        private HSlider _skillBarMarginBottomSlider;
        private Label _skillBarMarginBottomValue;
        private HSlider _skillBarNameFontSizeSlider;
        private Label _skillBarNameFontSizeValue;
        #endregion

        #region Fields - FunctionButtonBar Controls
        private HSlider _fnBarOffsetXSlider;
        private Label _fnBarOffsetXValue;
        private HSlider _fnBarOffsetYSlider;
        private Label _fnBarOffsetYValue;
        private HSlider _fnBarSpacingSlider;
        private Label _fnBarSpacingValue;
        #endregion

        public DebugPanelUITab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "ui";

        #region BuildUI
        public override void BuildUI(VBoxContainer tabContainer)
        {
            var title = new Label { Text = "UI 配置", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(title);
            tabContainer.AddChild(new HSeparator());

            // ── 技能栏 ──
            var skillBarTitle = new Label { Text = "技能栏", HorizontalAlignment = HorizontalAlignment.Left };
            skillBarTitle.AddThemeFontSizeOverride("font_size", 12);
            tabContainer.AddChild(skillBarTitle);

            // 查找 SkillBar
            var skillBar = Owner.GetTree()?.GetFirstNodeInGroup("skill_bar") as SkillBar;

            float iconSizeDefault = skillBar?.IconSize ?? 44;
            float spacingDefault = skillBar?.SlotSpacing ?? 6;
            float marginRDefault = skillBar?.MarginRight ?? 20;
            float marginBDefault = skillBar?.MarginBottom ?? 20;
            float nameFontSizeDefault = skillBar?.NameFontSize ?? 10;

            (_skillBarIconSizeSlider, _skillBarIconSizeValue) = CreateSliderRow(tabContainer, "图标大小", 24, 96, iconSizeDefault, 1f);
            (_skillBarSpacingSlider, _skillBarSpacingValue) = CreateSliderRow(tabContainer, "槽位间距", 0, 40, spacingDefault, 1f);
            (_skillBarNameFontSizeSlider, _skillBarNameFontSizeValue) = CreateSliderRow(tabContainer, "技能名字大小", 6, 28, nameFontSizeDefault, 1f);
            (_skillBarMarginRightSlider, _skillBarMarginRightValue) = CreateSliderRow(tabContainer, "右边距", 0, 200, marginRDefault, 1f);
            (_skillBarMarginBottomSlider, _skillBarMarginBottomValue) = CreateSliderRow(tabContainer, "底边距", 0, 200, marginBDefault, 1f);

            // ── 功能按钮栏 ──
            tabContainer.AddChild(new HSeparator());
            var fnBarTitle = new Label { Text = "功能按钮栏 (左上)", HorizontalAlignment = HorizontalAlignment.Left };
            fnBarTitle.AddThemeFontSizeOverride("font_size", 12);
            tabContainer.AddChild(fnBarTitle);

            var fnBar = Owner.GetTree()?.GetFirstNodeInGroup("function_bar") as FunctionButtonBar;
            float fnOffsetXDefault = fnBar?.OffsetX ?? 8;
            float fnOffsetYDefault = fnBar?.OffsetY ?? 8;
            float fnSpacingDefault = fnBar?.ButtonSpacing ?? 3;

            (_fnBarOffsetXSlider, _fnBarOffsetXValue) = CreateSliderRow(tabContainer, "水平偏移", 0, 300, fnOffsetXDefault, 1f);
            (_fnBarOffsetYSlider, _fnBarOffsetYValue) = CreateSliderRow(tabContainer, "垂直偏移", 0, 300, fnOffsetYDefault, 1f);
            (_fnBarSpacingSlider, _fnBarSpacingValue) = CreateSliderRow(tabContainer, "按钮间距", 0, 20, fnSpacingDefault, 1f);
        }
        #endregion

        #region Signal Connections
        public override void ConnectSignals()
        {
            if (_skillBarIconSizeSlider != null)
                _skillBarIconSizeSlider.ValueChanged += OnSkillBarIconSizeChanged;
            if (_skillBarSpacingSlider != null)
                _skillBarSpacingSlider.ValueChanged += OnSkillBarSpacingChanged;
            if (_skillBarNameFontSizeSlider != null)
                _skillBarNameFontSizeSlider.ValueChanged += OnSkillBarNameFontSizeChanged;
            if (_skillBarMarginRightSlider != null)
                _skillBarMarginRightSlider.ValueChanged += OnSkillBarMarginRightChanged;
            if (_skillBarMarginBottomSlider != null)
                _skillBarMarginBottomSlider.ValueChanged += OnSkillBarMarginBottomChanged;

            if (_fnBarOffsetXSlider != null)
                _fnBarOffsetXSlider.ValueChanged += OnFnBarOffsetXChanged;
            if (_fnBarOffsetYSlider != null)
                _fnBarOffsetYSlider.ValueChanged += OnFnBarOffsetYChanged;
            if (_fnBarSpacingSlider != null)
                _fnBarSpacingSlider.ValueChanged += OnFnBarSpacingChanged;
        }

        public override void DisconnectSignals()
        {
            if (_skillBarIconSizeSlider != null)
                _skillBarIconSizeSlider.ValueChanged -= OnSkillBarIconSizeChanged;
            if (_skillBarSpacingSlider != null)
                _skillBarSpacingSlider.ValueChanged -= OnSkillBarSpacingChanged;
            if (_skillBarNameFontSizeSlider != null)
                _skillBarNameFontSizeSlider.ValueChanged -= OnSkillBarNameFontSizeChanged;
            if (_skillBarMarginRightSlider != null)
                _skillBarMarginRightSlider.ValueChanged -= OnSkillBarMarginRightChanged;
            if (_skillBarMarginBottomSlider != null)
                _skillBarMarginBottomSlider.ValueChanged -= OnSkillBarMarginBottomChanged;

            if (_fnBarOffsetXSlider != null)
                _fnBarOffsetXSlider.ValueChanged -= OnFnBarOffsetXChanged;
            if (_fnBarOffsetYSlider != null)
                _fnBarOffsetYSlider.ValueChanged -= OnFnBarOffsetYChanged;
            if (_fnBarSpacingSlider != null)
                _fnBarSpacingSlider.ValueChanged -= OnFnBarSpacingChanged;
        }
        #endregion

        #region Handlers
        private void OnSkillBarIconSizeChanged(double value)
        {
            ApplySkillBarSettings();
        }

        private void OnSkillBarSpacingChanged(double value)
        {
            ApplySkillBarSettings();
        }

        private void OnSkillBarNameFontSizeChanged(double value)
        {
            ApplySkillBarSettings();
        }

        private void OnSkillBarMarginRightChanged(double value)
        {
            ApplySkillBarSettings();
        }

        private void OnSkillBarMarginBottomChanged(double value)
        {
            ApplySkillBarSettings();
        }

        private void ApplySkillBarSettings()
        {
            var skillBar = Owner.GetTree()?.GetFirstNodeInGroup("skill_bar") as SkillBar;
            if (skillBar == null) return;

            skillBar.IconSize = (int)_skillBarIconSizeSlider.Value;
            skillBar.SlotSpacing = (int)_skillBarSpacingSlider.Value;
            skillBar.NameFontSize = (int)_skillBarNameFontSizeSlider.Value;
            skillBar.MarginRight = (int)_skillBarMarginRightSlider.Value;
            skillBar.MarginBottom = (int)_skillBarMarginBottomSlider.Value;

            skillBar.RebuildLayout();
        }

        private void OnFnBarOffsetXChanged(double value)
        {
            ApplyFnBarSettings();
        }

        private void OnFnBarOffsetYChanged(double value)
        {
            ApplyFnBarSettings();
        }

        private void OnFnBarSpacingChanged(double value)
        {
            ApplyFnBarSettings();
        }

        private void ApplyFnBarSettings()
        {
            var fnBar = Owner.GetTree()?.GetFirstNodeInGroup("function_bar") as FunctionButtonBar;
            if (fnBar == null) return;

            fnBar.OffsetX = (float)_fnBarOffsetXSlider.Value;
            fnBar.OffsetY = (float)_fnBarOffsetYSlider.Value;
            fnBar.ButtonSpacing = (int)_fnBarSpacingSlider.Value;
            fnBar.RebuildLayout();
        }
        #endregion

        #region Config
        public override void SaveConfig(ConfigFile cfg)
        {
            cfg.SetValue("fn_bar", "offset_x", _fnBarOffsetXSlider?.Value ?? 8);
            cfg.SetValue("fn_bar", "offset_y", _fnBarOffsetYSlider?.Value ?? 8);
            cfg.SetValue("fn_bar", "spacing", _fnBarSpacingSlider?.Value ?? 3);
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded) return;

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

            ApplyFnBarSettings();
        }
        #endregion

        #region Sync
        public override void SyncToCurrentValues()
        {
            var skillBar = Owner.GetTree()?.GetFirstNodeInGroup("skill_bar") as SkillBar;
            if (skillBar == null) return;

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

            var fnBar = Owner.GetTree()?.GetFirstNodeInGroup("function_bar") as FunctionButtonBar;
            if (fnBar != null)
            {
                if (_fnBarOffsetXSlider != null)
                {
                    _fnBarOffsetXSlider.SetBlockSignals(true);
                    _fnBarOffsetXSlider.Value = fnBar.OffsetX;
                    _fnBarOffsetXSlider.SetBlockSignals(false);
                }
                if (_fnBarOffsetYSlider != null)
                {
                    _fnBarOffsetYSlider.SetBlockSignals(true);
                    _fnBarOffsetYSlider.Value = fnBar.OffsetY;
                    _fnBarOffsetYSlider.SetBlockSignals(false);
                }
                if (_fnBarSpacingSlider != null)
                {
                    _fnBarSpacingSlider.SetBlockSignals(true);
                    _fnBarSpacingSlider.Value = fnBar.ButtonSpacing;
                    _fnBarSpacingSlider.SetBlockSignals(false);
                }
            }
        }
        #endregion

        #region Undo
        public override Godot.Collections.Dictionary CaptureUndoState()
        {
            var state = new Godot.Collections.Dictionary
            {
                ["fn_bar_offset_x"] = _fnBarOffsetXSlider?.Value ?? 8.0,
                ["fn_bar_offset_y"] = _fnBarOffsetYSlider?.Value ?? 8.0,
                ["fn_bar_spacing"] = _fnBarSpacingSlider?.Value ?? 3.0,
            };
            return state;
        }

        public override void ApplyUndoState(Godot.Collections.Dictionary state)
        {
            if (_fnBarOffsetXSlider != null && state.TryGetValue("fn_bar_offset_x", out var ox))
            {
                _fnBarOffsetXSlider.SetBlockSignals(true);
                _fnBarOffsetXSlider.Value = (double)ox;
                _fnBarOffsetXSlider.SetBlockSignals(false);
            }
            if (_fnBarOffsetYSlider != null && state.TryGetValue("fn_bar_offset_y", out var oy))
            {
                _fnBarOffsetYSlider.SetBlockSignals(true);
                _fnBarOffsetYSlider.Value = (double)oy;
                _fnBarOffsetYSlider.SetBlockSignals(false);
            }
            if (_fnBarSpacingSlider != null && state.TryGetValue("fn_bar_spacing", out var sp))
            {
                _fnBarSpacingSlider.SetBlockSignals(true);
                _fnBarSpacingSlider.Value = (double)sp;
                _fnBarSpacingSlider.SetBlockSignals(false);
            }
            ApplyFnBarSettings();
        }
        #endregion
    }
}
