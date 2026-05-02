using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel Entity Style Tab 基类 — MonsterTab 和 NpcTab 的共同基类
    /// 提取了 Config ID Selector、Visual Style 滑块、Label Controls、
    /// Apply/Sync/Save/Load 公共样式逻辑
    /// </summary>
    public abstract class DebugPanelEntityStyleTabBase : DebugPanelTab
    {
        #region Fields - Config ID Selector
        protected SpinBox _configIdSpin;
        protected OptionButton _configIdOption;
        protected Button _addConfigBtn;
        protected Button _deleteConfigBtn;
        protected int _selectedConfigId = 1;

        /// <summary>切换到指定配置 ID（供外部调用，如实体点击选中）</summary>
        public void SelectConfigId(int id)
        {
            if (_configIdOption == null) return;
            for (int i = 0; i < _configIdOption.GetItemCount(); i++)
            {
                if ((int)_configIdOption.GetItemMetadata(i) == id)
                {
                    _configIdOption.Select(i);
                    _selectedConfigId = id;
                    SyncStyleUI();
                    SyncAfterStyleUI();
                    return;
                }
            }
        }
        #endregion

        #region Fields - Visual Style
        protected HSlider _sizeSlider;
        protected Label _sizeValue;
        protected HSlider _sizeScaleSlider;
        protected Label _sizeScaleValue;
        protected HSlider _borderWidthSlider;
        protected Label _borderWidthValue;
        protected HSlider _borderWidthScaleSlider;
        protected Label _borderWidthScaleValue;
        protected HSlider _cornerRadiusSlider;
        protected Label _cornerRadiusValue;
        protected HSlider _bgOpacitySlider;
        protected Label _bgOpacityValue;
        protected HSlider _fontSizeSlider;
        protected Label _fontSizeValue;

        protected ColorPickerButton _borderColorPicker;
        protected ColorPickerButton _bgColorPicker;
        protected ColorPickerButton _textColorPicker;
        #endregion

        #region Fields - Label Controls (4 independent)
        protected LineEdit[] _labelEdits = new LineEdit[4];
        protected HSlider[] _labelFontSizeSliders = new HSlider[4];
        protected Label[] _labelFontSizeValues = new Label[4];
        protected HSlider[] _labelXOffsetSliders = new HSlider[4];
        protected Label[] _labelXOffsetValues = new Label[4];
        protected HSlider[] _labelYOffsetSliders = new HSlider[4];
        protected Label[] _labelYOffsetValues = new Label[4];
        protected CheckButton[] _labelCenterXChecks = new CheckButton[4];
        #endregion

        protected DebugPanelEntityStyleTabBase(DebugPanel owner) : base(owner) { }

        #region Abstract — subclass must implement
        protected abstract string ConfigSectionPrefix { get; }
        protected abstract EntityStyleConfig GetStyleConfig(int id);
        protected abstract EntityStyleConfig GetOrCreateStyleConfig(int id);
        protected abstract Dictionary<int, EntityStyleConfig> GetAllStyleConfigs();
        protected abstract void ApplyStyleToAll();
        protected abstract void SyncAfterStyleUI();
        protected abstract void BuildSubclassUI(VBoxContainer tabContainer);
        protected abstract void ApplySubclassChanges(EntityStyleConfig cfg);
        protected abstract void SaveSubclassConfig(ConfigFile cfg, string section, EntityStyleConfig c);
        protected abstract void ExportSubclassConfigData(Godot.Collections.Dictionary dict, EntityStyleConfig c);
        #endregion

        #region BuildUI — Common Parts
        protected void BuildEntityStyleUI(VBoxContainer tabContainer, string titleText)
        {
            var title = new Label { Text = titleText, HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(title);
            tabContainer.AddChild(new HSeparator());
            BuildConfigIdSelector(tabContainer);
            tabContainer.AddChild(new HSeparator());

            (_sizeSlider, _sizeValue) = CreateMonsterSliderRow(tabContainer, "视觉大小", 32, 256, 111);
            (_sizeScaleSlider, _sizeScaleValue) = CreateMonsterSliderRow(tabContainer, "角色比例", 0.1f, 1.0f, 1.0f);
            (_borderWidthSlider, _borderWidthValue) = CreateMonsterSliderRow(tabContainer, "边框粗细", 0, 20, 3);
            (_borderWidthScaleSlider, _borderWidthScaleValue) = CreateMonsterSliderRow(tabContainer, "边框比例", 0.0f, 0.2f, 3.0f / 111.0f, DebugPanelLengthScalePolicy.StepF);
            (_cornerRadiusSlider, _cornerRadiusValue) = CreateMonsterSliderRow(tabContainer, "圆角半径", 0, 60, 12);
            (_bgOpacitySlider, _bgOpacityValue) = CreateMonsterSliderRow(tabContainer, "背景不透明度", 0, 1, 0.9f);
            (_fontSizeSlider, _fontSizeValue) = CreateMonsterSliderRow(tabContainer, "字体大小", 0, 48, 0);

            var bcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bcRow.AddChild(new Label { Text = "边框颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _borderColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(60, 26) };
            bcRow.AddChild(_borderColorPicker);
            tabContainer.AddChild(bcRow);

            var bgcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bgcRow.AddChild(new Label { Text = "背景颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _bgColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(60, 26) };
            bgcRow.AddChild(_bgColorPicker);
            tabContainer.AddChild(bgcRow);

            var tcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tcRow.AddChild(new Label { Text = "文字颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _textColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(60, 26) };
            tcRow.AddChild(_textColorPicker);
            tabContainer.AddChild(tcRow);

            tabContainer.AddChild(new HSeparator());
            BuildLabelControls(tabContainer);
        }

        protected void BuildLabelControls(VBoxContainer tabContainer)
        {
            tabContainer.AddChild(new Label { Text = "显示文字:" });
            for (int i = 0; i < 4; i++)
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(new Label { Text = $"行{i + 1}:", CustomMinimumSize = new Vector2(40, 0) });
                var edit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 26) };
                row.AddChild(edit);
                _labelEdits[i] = edit;
                tabContainer.AddChild(row);

                var fsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                fsRow.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) });
                fsRow.AddChild(new Label { Text = "字号:", CustomMinimumSize = new Vector2(36, 0) });
                var fsSlider = new HSlider { MinValue = 0, MaxValue = 48, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = 1, Scrollable = false };
                fsRow.AddChild(fsSlider);
                var fsVal = new Label { Text = "0", CustomMinimumSize = new Vector2(24, 0) };
                fsRow.AddChild(fsVal);
                fsSlider.ValueChanged += (v) => fsVal.Text = ((int)v).ToString();
                _labelFontSizeSliders[i] = fsSlider;
                _labelFontSizeValues[i] = fsVal;
                tabContainer.AddChild(fsRow);

                var xRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                xRow.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) });
                xRow.AddChild(new Label { Text = "X:", CustomMinimumSize = new Vector2(24, 0) });
                var xSlider = new HSlider { MinValue = -40, MaxValue = 40, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = 1, Scrollable = false };
                xRow.AddChild(xSlider);
                var xVal = new Label { Text = "0", CustomMinimumSize = new Vector2(28, 0) };
                xRow.AddChild(xVal);
                var centerCheck = new CheckButton { Text = "居中", ButtonPressed = true };
                xRow.AddChild(centerCheck);
                xSlider.ValueChanged += (v) => xVal.Text = ((int)v).ToString();
                centerCheck.Toggled += (enabled) => { xSlider.Editable = !enabled; xSlider.Modulate = enabled ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1); };
                _labelXOffsetSliders[i] = xSlider;
                _labelXOffsetValues[i] = xVal;
                _labelCenterXChecks[i] = centerCheck;
                tabContainer.AddChild(xRow);

                var yRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                yRow.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) });
                yRow.AddChild(new Label { Text = "Y:", CustomMinimumSize = new Vector2(24, 0) });
                var ySlider = new HSlider { MinValue = -40, MaxValue = 40, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Step = 1, Scrollable = false };
                yRow.AddChild(ySlider);
                var yVal = new Label { Text = "0", CustomMinimumSize = new Vector2(28, 0) };
                yRow.AddChild(yVal);
                ySlider.ValueChanged += (v) => yVal.Text = ((int)v).ToString();
                _labelYOffsetSliders[i] = ySlider;
                _labelYOffsetValues[i] = yVal;
                tabContainer.AddChild(yRow);
            }
        }

        protected void ConnectStyleSignals()
        {
            _sizeSlider.ValueChanged += (_) => ApplyStyleChanges();
            _sizeSlider.DragEnded += (_) => ApplyStyleChanges();
            _sizeScaleSlider.ValueChanged += (_) => ApplyStyleChanges();
            _sizeScaleSlider.DragEnded += (_) => ApplyStyleChanges();
            _borderWidthSlider.ValueChanged += (_) => ApplyStyleChanges();
            _borderWidthSlider.DragEnded += (_) => ApplyStyleChanges();
            _borderWidthScaleSlider.ValueChanged += (_) => ApplyStyleChanges();
            _borderWidthScaleSlider.DragEnded += (_) => ApplyStyleChanges();
            _cornerRadiusSlider.ValueChanged += (_) => ApplyStyleChanges();
            _cornerRadiusSlider.DragEnded += (_) => ApplyStyleChanges();
            _bgOpacitySlider.ValueChanged += (_) => ApplyStyleChanges();
            _bgOpacitySlider.DragEnded += (_) => ApplyStyleChanges();
            _fontSizeSlider.ValueChanged += (_) => ApplyStyleChanges();
            _fontSizeSlider.DragEnded += (_) => ApplyStyleChanges();
            _borderColorPicker.ColorChanged += (_) => ApplyStyleChanges();
            _bgColorPicker.ColorChanged += (_) => ApplyStyleChanges();
            _textColorPicker.ColorChanged += (_) => ApplyStyleChanges();
            for (int i = 0; i < 4; i++)
            {
                _labelEdits[i].TextChanged += (_) => ApplyStyleChanges();
                _labelFontSizeSliders[i].ValueChanged += (_) => ApplyStyleChanges();
                _labelFontSizeSliders[i].DragEnded += (_) => ApplyStyleChanges();
                _labelXOffsetSliders[i].ValueChanged += (_) => ApplyStyleChanges();
                _labelXOffsetSliders[i].DragEnded += (_) => ApplyStyleChanges();
                _labelCenterXChecks[i].Toggled += (_) => ApplyStyleChanges();
                _labelYOffsetSliders[i].ValueChanged += (_) => ApplyStyleChanges();
                _labelYOffsetSliders[i].DragEnded += (_) => ApplyStyleChanges();
            }
        }
        #endregion

        #region Config ID Selector
        private void BuildConfigIdSelector(Container parent)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = "配置ID:", CustomMinimumSize = new Vector2(56, 0) });
            _configIdSpin = new SpinBox { MinValue = 1, MaxValue = 99999, Step = 1, Value = 1, CustomMinimumSize = new Vector2(60, 0) };
            row.AddChild(_configIdSpin);
            _configIdOption = new OptionButton { CustomMinimumSize = new Vector2(80, 0) };
            row.AddChild(_configIdOption);
            _addConfigBtn = new Button { Text = "新增", CustomMinimumSize = new Vector2(44, 0) };
            row.AddChild(_addConfigBtn);
            _deleteConfigBtn = new Button { Text = "删除", CustomMinimumSize = new Vector2(44, 0) };
            row.AddChild(_deleteConfigBtn);
            parent.AddChild(row);
            _configIdOption.ItemSelected += OnConfigIdOptionSelected;
            _addConfigBtn.Pressed += OnAddConfigPressed;
            _deleteConfigBtn.Pressed += OnDeleteConfigPressed;
        }

        protected void RefreshConfigIdList()
        {
            var configs = GetAllStyleConfigs();
            _configIdOption.Clear();
            foreach (var kv in configs)
            {
                int idx = _configIdOption.GetItemCount();
                _configIdOption.AddItem(kv.Key.ToString());
                _configIdOption.SetItemMetadata(idx, kv.Key);
            }
            for (int i = 0; i < _configIdOption.GetItemCount(); i++)
            {
                if ((int)_configIdOption.GetItemMetadata(i) == _selectedConfigId)
                {
                    _configIdOption.Select(i);
                    break;
                }
            }
        }

        private void OnConfigIdOptionSelected(long index)
        {
            if (index < 0 || index >= _configIdOption.GetItemCount()) return;
            _selectedConfigId = (int)_configIdOption.GetItemMetadata((int)index);
            _configIdSpin.Value = _selectedConfigId;
            SyncStyleUI();
            SyncAfterStyleUI();
        }

        private void OnAddConfigPressed()
        {
            int newId = (int)_configIdSpin.Value;
            var configs = GetAllStyleConfigs();
            if (configs.ContainsKey(newId))
            {
                _selectedConfigId = newId;
                RefreshConfigIdList();
                SyncStyleUI();
                SyncAfterStyleUI();
                return;
            }
            GetOrCreateStyleConfig(newId);
            _selectedConfigId = newId;
            RefreshConfigIdList();
            SyncStyleUI();
            SyncAfterStyleUI();
        }

        private void OnDeleteConfigPressed()
        {
            var configs = GetAllStyleConfigs();
            if (configs.Count <= 1) return;
            if (!configs.ContainsKey(_selectedConfigId)) return;
            configs.Remove(_selectedConfigId);
            _selectedConfigId = configs.Keys.First();
            RefreshConfigIdList();
            SyncStyleUI();
            SyncAfterStyleUI();
            ApplyStyleToAll();
        }
        #endregion

        #region Apply Style Changes — Common
        protected void ApplyStyleChanges()
        {
            var cfg = GetOrCreateStyleConfig(_selectedConfigId);
            cfg.VisualSizeScale = (float)_sizeScaleSlider.Value;
            cfg.BorderWidthScale = (float)_borderWidthScaleSlider.Value;
            cfg.CornerRadius = (float)_cornerRadiusSlider.Value;
            cfg.BgOpacity = (float)_bgOpacitySlider.Value;
            cfg.FontSize = (int)_fontSizeSlider.Value;
            cfg.BorderColor = _borderColorPicker.Color;
            cfg.BgColor = _bgColorPicker.Color;
            cfg.TextColor = _textColorPicker.Color;
            for (int i = 0; i < 4; i++)
            {
                cfg.LabelTexts[i] = _labelEdits[i].Text;
                cfg.LabelFontSizes[i] = (int)_labelFontSizeSliders[i].Value;
                cfg.LabelXOffsets[i] = (float)_labelXOffsetSliders[i].Value;
                cfg.LabelCenterX[i] = _labelCenterXChecks[i].ButtonPressed;
                cfg.LabelYOffsets[i] = (float)_labelYOffsetSliders[i].Value;
            }
            ApplySubclassChanges(cfg);
            ApplyStyleToAll();
        }
        #endregion

        #region Sync Style UI — Common
        protected void SyncStyleUI()
        {
            var cfg = GetStyleConfig(_selectedConfigId);

            _sizeSlider.SetBlockSignals(true);
            _sizeScaleSlider.SetBlockSignals(true);
            _borderWidthSlider.SetBlockSignals(true);
            _borderWidthScaleSlider.SetBlockSignals(true);
            _cornerRadiusSlider.SetBlockSignals(true);
            _bgOpacitySlider.SetBlockSignals(true);
            _fontSizeSlider.SetBlockSignals(true);

            _sizeScaleSlider.Value = cfg.VisualSizeScale;
            _sizeScaleValue.Text = cfg.VisualSizeScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            _borderWidthScaleSlider.Value = cfg.BorderWidthScale;
            _borderWidthScaleValue.Text = cfg.BorderWidthScale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            _cornerRadiusSlider.Value = cfg.CornerRadius;
            _bgOpacitySlider.Value = cfg.BgOpacity;
            _fontSizeSlider.Value = cfg.FontSize;
            _borderColorPicker.Color = cfg.BorderColor;
            _bgColorPicker.Color = cfg.BgColor;
            _textColorPicker.Color = cfg.TextColor;

            _sizeSlider.SetBlockSignals(false);
            _sizeScaleSlider.SetBlockSignals(false);
            _borderWidthSlider.SetBlockSignals(false);
            _borderWidthScaleSlider.SetBlockSignals(false);
            _cornerRadiusSlider.SetBlockSignals(false);
            _bgOpacitySlider.SetBlockSignals(false);
            _fontSizeSlider.SetBlockSignals(false);

            for (int i = 0; i < 4; i++)
            {
                _labelEdits[i].Text = cfg.LabelTexts[i] ?? "";
                _labelFontSizeSliders[i].Value = cfg.LabelFontSizes[i];
                _labelFontSizeValues[i].Text = cfg.LabelFontSizes[i].ToString();
                _labelXOffsetSliders[i].Value = cfg.LabelXOffsets[i];
                _labelXOffsetValues[i].Text = cfg.LabelXOffsets[i].ToString("F0");
                _labelCenterXChecks[i].ButtonPressed = cfg.LabelCenterX[i];
                _labelXOffsetSliders[i].Editable = !cfg.LabelCenterX[i];
                _labelXOffsetSliders[i].Modulate = cfg.LabelCenterX[i] ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                _labelYOffsetSliders[i].Value = cfg.LabelYOffsets[i];
                _labelYOffsetValues[i].Text = cfg.LabelYOffsets[i].ToString("F0");
            }
        }
        #endregion

        #region SaveConfig / LoadConfig — Common Style Fields
        protected void SaveCommonStyleConfig(ConfigFile cfg, string section, EntityStyleConfig c)
        {
            cfg.SetValue(section, "visual_size_scale", (double)c.VisualSizeScale);
            cfg.SetValue(section, "border_width_scale", (double)c.BorderWidthScale);
            cfg.SetValue(section, "corner_radius", (double)c.CornerRadius);
            cfg.SetValue(section, "bg_opacity", (double)c.BgOpacity);
            cfg.SetValue(section, "font_size", (double)c.FontSize);
            cfg.SetValue(section, "border_color_r", (double)c.BorderColor.R);
            cfg.SetValue(section, "border_color_g", (double)c.BorderColor.G);
            cfg.SetValue(section, "border_color_b", (double)c.BorderColor.B);
            cfg.SetValue(section, "bg_color_r", (double)c.BgColor.R);
            cfg.SetValue(section, "bg_color_g", (double)c.BgColor.G);
            cfg.SetValue(section, "bg_color_b", (double)c.BgColor.B);
            cfg.SetValue(section, "text_color_r", (double)c.TextColor.R);
            cfg.SetValue(section, "text_color_g", (double)c.TextColor.G);
            cfg.SetValue(section, "text_color_b", (double)c.TextColor.B);
            for (int i = 0; i < 4; i++)
            {
                cfg.SetValue(section, $"label_text_{i}", c.LabelTexts[i] ?? "");
                cfg.SetValue(section, $"label_font_size_{i}", (double)c.LabelFontSizes[i]);
                cfg.SetValue(section, $"label_x_offset_{i}", (double)c.LabelXOffsets[i]);
                cfg.SetValue(section, $"label_center_x_{i}", c.LabelCenterX[i]);
                cfg.SetValue(section, $"label_y_offset_{i}", (double)c.LabelYOffsets[i]);
            }
        }

        protected void SaveBarConfig(ConfigFile cfg, string section, string prefix, EntityStyleConfig c)
        {
            bool isHp = prefix == "hp";
            bool visible = isHp ? c.HpBarVisible : c.MpBarVisible;
            float lengthScale = isHp ? c.HpBarLengthScale : c.MpBarLengthScale;
            float heightScale = isHp ? c.HpBarHeightScale : c.MpBarHeightScale;
            float fillPercent = isHp ? c.HpBarFillPercent : c.MpBarFillPercent;
            bool centerX = isHp ? c.HpBarCenterX : c.MpBarCenterX;
            float offsetX = isHp ? c.HpBarOffsetX : c.MpBarOffsetX;
            float offsetY = isHp ? c.HpBarOffsetY : c.MpBarOffsetY;
            Color color = isHp ? c.HpBarColor : c.MpBarColor;

            cfg.SetValue(section, $"{prefix}_bar_visible", visible);
            cfg.SetValue(section, $"{prefix}_bar_length_scale", (double)lengthScale);
            cfg.SetValue(section, $"{prefix}_bar_height_scale", (double)heightScale);
            cfg.SetValue(section, $"{prefix}_bar_fill_percent", (double)fillPercent);
            cfg.SetValue(section, $"{prefix}_bar_center_x", centerX);
            cfg.SetValue(section, $"{prefix}_bar_offset_x", (double)offsetX);
            cfg.SetValue(section, $"{prefix}_bar_offset_y", (double)offsetY);
            cfg.SetValue(section, $"{prefix}_bar_color_r", (double)color.R);
            cfg.SetValue(section, $"{prefix}_bar_color_g", (double)color.G);
            cfg.SetValue(section, $"{prefix}_bar_color_b", (double)color.B);
        }
        #endregion

        #region ExportConfigData — Common
        protected void ExportCommonConfigData(Godot.Collections.Dictionary dict, EntityStyleConfig c)
        {
            dict["visual_size_scale"] = c.VisualSizeScale;
            dict["border_width_scale"] = c.BorderWidthScale;
            dict["corner_radius"] = c.CornerRadius;
            dict["bg_opacity"] = c.BgOpacity;
            dict["font_size"] = c.FontSize;
        }
        #endregion

        #region ConnectSignals / DisconnectSignals
        public override void ConnectSignals() { }
        public override void DisconnectSignals() { }
        #endregion

    }
}
