using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 外观组件 — 大小/比例（联动）、边框/比例（联动）、圆角、背景不透明度、字体大小、颜色选择器
    /// </summary>
    public class AppearanceComponent : IEntityTabComponent
    {
        public string ComponentName => "appearance";
        public string DisplayName => "外观";
        public Type DataType => typeof(AppearanceData);

        private Action _onChanged;

        // Size + Scale (linked)
        private HSlider _sizeSlider;
        private Label _sizeValue;
        private HSlider _sizeScaleSlider;
        private Label _sizeScaleValue;

        // BorderWidth + Scale (linked)
        private HSlider _borderWidthSlider;
        private Label _borderWidthValue;
        private HSlider _borderWidthScaleSlider;
        private Label _borderWidthScaleValue;

        // Other
        private HSlider _cornerRadiusSlider;
        private Label _cornerRadiusValue;
        private HSlider _bgOpacitySlider;
        private Label _bgOpacityValue;
        private HSlider _fontSizeSlider;
        private Label _fontSizeValue;

        // Colors
        private ColorPickerButton _borderColorPicker;
        private ColorPickerButton _bgColorPicker;
        private ColorPickerButton _textColorPicker;

        // Grid size reference for linkage calculations
        private int _gridSize = 111;

        public void BuildUI(VBoxContainer parent)
        {
            // Size + Scale
            (_sizeSlider, _sizeValue) = CreateSliderRow(parent, "角色大小", 32, 256, 111, 1f);
            (_sizeScaleSlider, _sizeScaleValue) = CreateSliderRow(parent, "角色比例", 0.1f, 1.0f, 1.0f, DebugPanelLengthScalePolicy.StepF);

            // BorderWidth + Scale
            (_borderWidthSlider, _borderWidthValue) = CreateSliderRow(parent, "边框粗细", 1.0f, 20.0f, 3.0f, 0.5f);
            (_borderWidthScaleSlider, _borderWidthScaleValue) = CreateSliderRow(parent, "边框比例", 0.0f, 0.2f, 3.0f / 111.0f, DebugPanelLengthScalePolicy.StepF);

            // Other
            (_cornerRadiusSlider, _cornerRadiusValue) = CreateSliderRow(parent, "圆角半径", 0.0f, 60.0f, 12.0f, 1f);
            (_bgOpacitySlider, _bgOpacityValue) = CreateSliderRow(parent, "背景不透明度", 0.0f, 1.0f, 0.9f, DebugPanelLengthScalePolicy.StepF);
            (_fontSizeSlider, _fontSizeValue) = CreateSliderRow(parent, "字体大小", 0, 48, 0, 1f);

            // Colors
            var bcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bcRow.AddChild(new Label { Text = "边框颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _borderColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(60, 26) };
            bcRow.AddChild(_borderColorPicker);
            parent.AddChild(bcRow);

            var bgcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bgcRow.AddChild(new Label { Text = "背景颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _bgColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(60, 26) };
            bgcRow.AddChild(_bgColorPicker);
            parent.AddChild(bgcRow);

            var tcRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tcRow.AddChild(new Label { Text = "文字颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _textColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(60, 26) };
            tcRow.AddChild(_textColorPicker);
            parent.AddChild(tcRow);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not AppearanceData d) return;

            SetSliderSilent(_sizeScaleSlider, d.VisualSizeScale, _sizeScaleValue, d.VisualSizeScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            SetSliderSilent(_borderWidthScaleSlider, d.BorderWidthScale, _borderWidthScaleValue, d.BorderWidthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            SetSliderSilent(_cornerRadiusSlider, d.CornerRadius, _cornerRadiusValue, d.CornerRadius.ToString("F0"));
            SetSliderSilent(_bgOpacitySlider, d.BgOpacity, _bgOpacityValue, d.BgOpacity.ToString(DebugPanelLengthScalePolicy.FormatStr));
            SetSliderSilent(_fontSizeSlider, d.FontSize, _fontSizeValue, ((int)d.FontSize).ToString());

            // Compute linked values
            _gridSize = (int)_sizeSlider.MaxValue; // approximate; real gridSize comes from entity
            SetSliderSilent(_sizeSlider, _gridSize * d.VisualSizeScale, _sizeValue, ((int)(_gridSize * d.VisualSizeScale)).ToString());
            SetSliderSilent(_borderWidthSlider, _gridSize * d.BorderWidthScale, _borderWidthValue, ((int)(_gridSize * d.BorderWidthScale)).ToString());

            _borderColorPicker.Color = d.BorderColor;
            _bgColorPicker.Color = d.BgColor;
            _textColorPicker.Color = d.TextColor;
        }

        public IComponentData SyncToData()
        {
            return new AppearanceData
            {
                VisualSizeScale = EntityProfileManager.FromFp(EntityProfileManager.ToFpD(_sizeScaleSlider.Value)),
                BorderWidthScale = EntityProfileManager.FromFp(EntityProfileManager.ToFpD(_borderWidthScaleSlider.Value)),
                CornerRadius = (float)_cornerRadiusSlider.Value,
                BgOpacity = EntityProfileManager.FromFp(EntityProfileManager.ToFpD(_bgOpacitySlider.Value)),
                FontSize = (int)_fontSizeSlider.Value,
                BorderColor = _borderColorPicker.Color,
                BgColor = _bgColorPicker.Color,
                TextColor = _textColorPicker.Color,
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;

            // Size linkage: immediate, including manual LineEdit apply
            _sizeSlider.ValueChanged += OnSizeChanged;
            _sizeScaleSlider.ValueChanged += OnSizeScaleChanged;

            // BorderWidth linkage: immediate, including manual LineEdit apply
            _borderWidthSlider.ValueChanged += OnBorderWidthChanged;
            _borderWidthScaleSlider.ValueChanged += OnBorderWidthScaleChanged;

            // Simple sliders
            _cornerRadiusSlider.ValueChanged += OnSimpleChanged;
            _bgOpacitySlider.ValueChanged += OnSimpleChanged;
            _fontSizeSlider.ValueChanged += OnSimpleChanged;

            // Colors
            _borderColorPicker.ColorChanged += OnColorChanged;
            _bgColorPicker.ColorChanged += OnColorChanged;
            _textColorPicker.ColorChanged += OnColorChanged;
        }

        public void DisconnectSignals()
        {
            _sizeSlider.ValueChanged -= OnSizeChanged;
            _sizeScaleSlider.ValueChanged -= OnSizeScaleChanged;
            _borderWidthSlider.ValueChanged -= OnBorderWidthChanged;
            _borderWidthScaleSlider.ValueChanged -= OnBorderWidthScaleChanged;
            _cornerRadiusSlider.ValueChanged -= OnSimpleChanged;
            _bgOpacitySlider.ValueChanged -= OnSimpleChanged;
            _fontSizeSlider.ValueChanged -= OnSimpleChanged;
            _borderColorPicker.ColorChanged -= OnColorChanged;
            _bgColorPicker.ColorChanged -= OnColorChanged;
            _textColorPicker.ColorChanged -= OnColorChanged;
        }

        public void SyncFromEntity(EntityBase entity)
        {
            if (entity == null) return;
            _gridSize = entity.GridSize;

            SetSliderSilent(_sizeScaleSlider, entity.VisualSizeScale, _sizeScaleValue, entity.VisualSizeScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            SetSliderSilent(_sizeSlider, _gridSize * entity.VisualSizeScale, _sizeValue, ((int)(_gridSize * entity.VisualSizeScale)).ToString());
            SetSliderSilent(_borderWidthScaleSlider, entity.BorderWidthScale, _borderWidthScaleValue, entity.BorderWidthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            SetSliderSilent(_borderWidthSlider, _gridSize * entity.BorderWidthScale, _borderWidthValue, ((int)(_gridSize * entity.BorderWidthScale)).ToString());
            SetSliderSilent(_cornerRadiusSlider, entity.CornerRadius, _cornerRadiusValue, entity.CornerRadius.ToString("F0"));
            SetSliderSilent(_bgOpacitySlider, entity.BgOpacity, _bgOpacityValue, entity.BgOpacity.ToString(DebugPanelLengthScalePolicy.FormatStr));
            SetSliderSilent(_fontSizeSlider, entity.FontSize, _fontSizeValue, entity.FontSize.ToString());

            _borderColorPicker.Color = entity.BorderColor;
            _bgColorPicker.Color = entity.BgColor;
            _textColorPicker.Color = entity.TextColor;
        }

        public void SetPropertyLocked(string propertyName, bool locked)
        {
            // No specific lockable properties for appearance currently
        }

        public void SetCollapsed(bool collapsed) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }

        public void Dispose()
        {
            DisconnectSignals();
        }

        private void OnSimpleChanged(double _) => _onChanged?.Invoke();
        private void OnColorChanged(Color _) => _onChanged?.Invoke();

        #region Size Linkage
        private void OnSizeChanged(double value)
        {
            _sizeValue.Text = ((int)value).ToString();
            if (_gridSize > 0)
            {
                double scale = Math.Clamp(_sizeSlider.Value / _gridSize, _sizeScaleSlider.MinValue, _sizeScaleSlider.MaxValue);
                _sizeScaleSlider.SetBlockSignals(true);
                _sizeScaleSlider.Value = scale;
                _sizeScaleSlider.SetBlockSignals(false);
                _sizeScaleValue.Text = scale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            }
            _onChanged?.Invoke();
        }

        private void OnSizeScaleChanged(double value)
        {
            _sizeScaleValue.Text = value.ToString(DebugPanelLengthScalePolicy.FormatStr);
            if (_gridSize > 0)
            {
                double newSize = Math.Clamp(_sizeScaleSlider.Value * _gridSize, _sizeSlider.MinValue, _sizeSlider.MaxValue);
                _sizeSlider.SetBlockSignals(true);
                _sizeSlider.Value = newSize;
                _sizeSlider.SetBlockSignals(false);
                _sizeValue.Text = ((int)newSize).ToString();
            }
            _onChanged?.Invoke();
        }
        #endregion

        #region BorderWidth Linkage
        private void OnBorderWidthChanged(double value)
        {
            _borderWidthValue.Text = ((int)value).ToString();
            if (_gridSize > 0)
            {
                double scale = Math.Clamp(_borderWidthSlider.Value / _gridSize, _borderWidthScaleSlider.MinValue, _borderWidthScaleSlider.MaxValue);
                _borderWidthScaleSlider.SetBlockSignals(true);
                _borderWidthScaleSlider.Value = scale;
                _borderWidthScaleSlider.SetBlockSignals(false);
                _borderWidthScaleValue.Text = scale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            }
            _onChanged?.Invoke();
        }

        private void OnBorderWidthScaleChanged(double value)
        {
            _borderWidthScaleValue.Text = value.ToString(DebugPanelLengthScalePolicy.FormatStr);
            if (_gridSize > 0)
            {
                double newWidth = Math.Clamp(_borderWidthScaleSlider.Value * _gridSize, _borderWidthSlider.MinValue, _borderWidthSlider.MaxValue);
                _borderWidthSlider.SetBlockSignals(true);
                _borderWidthSlider.Value = newWidth;
                _borderWidthSlider.SetBlockSignals(false);
                _borderWidthValue.Text = ((int)newWidth).ToString();
            }
            _onChanged?.Invoke();
        }
        #endregion

        #region Helpers
        private static (HSlider slider, Label valueLabel) CreateSliderRow(
            Container parent, string label, float min, float max, float def, float step)
        {
            float actualStep = step > 0 ? step : (max <= 1.0f ? 0.05f : 1f);
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label + ":", CustomMinimumSize = new Vector2(80, 0) });

            var slider = new HSlider
            {
                MinValue = min,
                MaxValue = max,
                Value = def,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 20),
                Step = actualStep,
                FocusMode = Control.FocusModeEnum.Click,
                Scrollable = false
            };
            row.AddChild(slider);

            string initialText = actualStep < 1.0f ? def.ToString(DebugPanelLengthScalePolicy.FormatStr) : ((int)def).ToString();
            var valLbl = new Label { Text = initialText, CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(valLbl);

            Func<double, string> fmt = actualStep < 1.0f
                ? (v => v.ToString(DebugPanelLengthScalePolicy.FormatStr))
                : (v => ((int)v).ToString());
            slider.ValueChanged += (v) => valLbl.Text = fmt(v);
            parent.AddChild(row);
            SliderValueInput.Attach(slider, valLbl, fmt);
            return (slider, valLbl);
        }

        private static void SetSliderSilent(HSlider slider, double value, Label label, string text)
        {
            slider.SetBlockSignals(true);
            slider.Value = value;
            slider.SetBlockSignals(false);
            if (label != null) label.Text = text;
        }
        #endregion
    }
}
