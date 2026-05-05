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

        #region Fields - BuffBar Controls
        private HSlider _buffBarIconSizeSlider;
        private Label _buffBarIconSizeValue;
        private HSlider _buffBarSpacingSlider;
        private Label _buffBarSpacingValue;
        private HSlider _buffBarOffsetXSlider;
        private Label _buffBarOffsetXValue;
        private HSlider _buffBarOffsetYSlider;
        private Label _buffBarOffsetYValue;
        private Button _buffBarForceShowBtn;
        private CheckBox _buffBarRightAlignCheck;
        #endregion

        public DebugPanelUITab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "ui";

        #region BuildUI
        public override void BuildUI(VBoxContainer tabContainer)
        {
            _tabContainer = tabContainer;
            var title = new Label { Name = "_lbl", Text = "UI 配置", HorizontalAlignment = HorizontalAlignment.Center };
            title.Name = "_lbl";
            title.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(title);
            tabContainer.AddChild(new HSeparator());

            // ── 技能栏 ──
            var skillBarTitle = new Label { Name = "_lbl", Text = "技能栏", HorizontalAlignment = HorizontalAlignment.Left };
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
            var fnBarTitle = new Label { Name = "_lbl", Text = "功能按钮栏 (左上)", HorizontalAlignment = HorizontalAlignment.Left };
            fnBarTitle.AddThemeFontSizeOverride("font_size", 12);
            tabContainer.AddChild(fnBarTitle);

            var fnBar = Owner.GetTree()?.GetFirstNodeInGroup("function_bar") as FunctionButtonBar;
            float fnOffsetXDefault = fnBar?.OffsetX ?? 8;
            float fnOffsetYDefault = fnBar?.OffsetY ?? 8;
            float fnSpacingDefault = fnBar?.ButtonSpacing ?? 3;

            (_fnBarOffsetXSlider, _fnBarOffsetXValue) = CreateSliderRow(tabContainer, "水平偏移", 0, 300, fnOffsetXDefault, 1f);
            (_fnBarOffsetYSlider, _fnBarOffsetYValue) = CreateSliderRow(tabContainer, "垂直偏移", 0, 300, fnOffsetYDefault, 1f);
            (_fnBarSpacingSlider, _fnBarSpacingValue) = CreateSliderRow(tabContainer, "按钮间距", 0, 20, fnSpacingDefault, 1f);

            // ── Buff 栏 ──
            tabContainer.AddChild(new HSeparator());
            var buffBarTitle = new Label { Name = "_lbl", Text = "Buff 栏", HorizontalAlignment = HorizontalAlignment.Left };
            buffBarTitle.AddThemeFontSizeOverride("font_size", 12);
            tabContainer.AddChild(buffBarTitle);

            var buffBar = Owner.GetTree()?.GetFirstNodeInGroup("buff_bar") as BuffBar;
            float buffIconSizeDefault = buffBar?.IconSize ?? 36;
            float buffSpacingDefault = buffBar?.SlotSpacing ?? 4;

            (_buffBarIconSizeSlider, _buffBarIconSizeValue) = CreateSliderRow(tabContainer, "图标大小", 16, 64, buffIconSizeDefault, 1f);
            (_buffBarSpacingSlider, _buffBarSpacingValue) = CreateSliderRow(tabContainer, "槽位间距", 0, 20, buffSpacingDefault, 1f);

            float buffOffsetXDefault = buffBar?.OffsetX ?? 0;
            float buffOffsetYDefault = buffBar?.OffsetY ?? 0;
            (_buffBarOffsetXSlider, _buffBarOffsetXValue) = CreateSliderRow(tabContainer, "X 偏移", -500, 500, buffOffsetXDefault, 1f);
            (_buffBarOffsetYSlider, _buffBarOffsetYValue) = CreateSliderRow(tabContainer, "Y 偏移", -500, 500, buffOffsetYDefault, 1f);

            _buffBarForceShowBtn = new Button { Text = "强制显示" };
            _buffBarForceShowBtn.Pressed += OnBuffBarForceShow;
            tabContainer.AddChild(_buffBarForceShowBtn);

            _buffBarRightAlignCheck = new CheckBox { Text = "靠右对齐（新buff压栈）" };
            _buffBarRightAlignCheck.ButtonPressed = buffBar?.RightAlign ?? false;
            _buffBarRightAlignCheck.Toggled += OnBuffBarRightAlignToggled;
            tabContainer.AddChild(_buffBarRightAlignCheck);
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

            if (_buffBarIconSizeSlider != null)
                _buffBarIconSizeSlider.ValueChanged += OnBuffBarIconSizeChanged;
            if (_buffBarSpacingSlider != null)
                _buffBarSpacingSlider.ValueChanged += OnBuffBarSpacingChanged;
            if (_buffBarOffsetXSlider != null)
                _buffBarOffsetXSlider.ValueChanged += OnBuffBarOffsetXChanged;
            if (_buffBarOffsetYSlider != null)
                _buffBarOffsetYSlider.ValueChanged += OnBuffBarOffsetYChanged;
            if (_buffBarRightAlignCheck != null)
                _buffBarRightAlignCheck.Toggled += OnBuffBarRightAlignToggled;
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

            if (_buffBarIconSizeSlider != null)
                _buffBarIconSizeSlider.ValueChanged -= OnBuffBarIconSizeChanged;
            if (_buffBarSpacingSlider != null)
                _buffBarSpacingSlider.ValueChanged -= OnBuffBarSpacingChanged;
            if (_buffBarOffsetXSlider != null)
                _buffBarOffsetXSlider.ValueChanged -= OnBuffBarOffsetXChanged;
            if (_buffBarOffsetYSlider != null)
                _buffBarOffsetYSlider.ValueChanged -= OnBuffBarOffsetYChanged;
            if (_buffBarRightAlignCheck != null)
                _buffBarRightAlignCheck.Toggled -= OnBuffBarRightAlignToggled;
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

        private void OnBuffBarIconSizeChanged(double value) => ApplyBuffBarSettings();
        private void OnBuffBarSpacingChanged(double value) => ApplyBuffBarSettings();
        private void OnBuffBarOffsetXChanged(double value) => ApplyBuffBarSettings();
        private void OnBuffBarOffsetYChanged(double value) => ApplyBuffBarSettings();

        private void OnBuffBarForceShow()
        {
            var buffBar = Owner.GetTree()?.GetFirstNodeInGroup("buff_bar") as BuffBar;
            buffBar?.ForceShowAll();
        }

        private void OnBuffBarRightAlignToggled(bool pressed)
        {
            var buffBar = Owner.GetTree()?.GetFirstNodeInGroup("buff_bar") as BuffBar;
            if (buffBar == null) return;
            buffBar.RightAlign = pressed;
            buffBar.RebuildLayout();
        }

        private void ApplyBuffBarSettings()
        {
            var buffBar = Owner.GetTree()?.GetFirstNodeInGroup("buff_bar") as BuffBar;
            if (buffBar == null) return;

            buffBar.IconSize = (int)_buffBarIconSizeSlider.Value;
            buffBar.SlotSpacing = (int)_buffBarSpacingSlider.Value;
            buffBar.OffsetX = (int)_buffBarOffsetXSlider.Value;
            buffBar.OffsetY = (int)_buffBarOffsetYSlider.Value;
            buffBar.RightAlign = _buffBarRightAlignCheck?.ButtonPressed ?? false;
            buffBar.RebuildLayout();
        }
        #endregion

        #region Config
        public override void SaveConfig(ConfigFile cfg)
        {
            // 技能栏
            cfg.SetValue("skill_bar", "icon_size", _skillBarIconSizeSlider?.Value ?? 44);
            cfg.SetValue("skill_bar", "spacing", _skillBarSpacingSlider?.Value ?? 6);
            cfg.SetValue("skill_bar", "name_font_size", _skillBarNameFontSizeSlider?.Value ?? 10);
            cfg.SetValue("skill_bar", "margin_right", _skillBarMarginRightSlider?.Value ?? 20);
            cfg.SetValue("skill_bar", "margin_bottom", _skillBarMarginBottomSlider?.Value ?? 20);

            // 功能按钮栏
            cfg.SetValue("fn_bar", "offset_x", _fnBarOffsetXSlider?.Value ?? 8);
            cfg.SetValue("fn_bar", "offset_y", _fnBarOffsetYSlider?.Value ?? 8);
            cfg.SetValue("fn_bar", "spacing", _fnBarSpacingSlider?.Value ?? 3);

            // Buff 栏
            cfg.SetValue("buff_bar", "icon_size", _buffBarIconSizeSlider?.Value ?? 36);
            cfg.SetValue("buff_bar", "spacing", _buffBarSpacingSlider?.Value ?? 4);
            cfg.SetValue("buff_bar", "offset_x", _buffBarOffsetXSlider?.Value ?? 0);
            cfg.SetValue("buff_bar", "offset_y", _buffBarOffsetYSlider?.Value ?? 0);
            cfg.SetValue("buff_bar", "right_align", _buffBarRightAlignCheck?.ButtonPressed ?? false);
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded) return;

            // 技能栏
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
            ApplySkillBarSettings();

            // 功能按钮栏
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

            // Buff 栏
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
            ApplyBuffBarSettings();
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

            // Buff 栏
            var buffBar = Owner.GetTree()?.GetFirstNodeInGroup("buff_bar") as BuffBar;
            if (buffBar != null)
            {
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
        #endregion

    }
}
