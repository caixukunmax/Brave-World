using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 铭牌背景组件 —— 调试面板中配置三层填充色块（顶栏/中块/底栏）。
    /// 顶栏与底栏共用高度与颜色；中块宽度按实体外框比例居中缩放。
    /// </summary>
    public class NameplateComponent : IEntityTabComponent
    {
        public string ComponentName => "nameplate";
        public string DisplayName => "铭牌背景";
        public Type DataType => typeof(NameplateData);

        private Action _onChanged;

        private CheckButton _visibleCheck;

        private HSlider _yOffsetSlider;
        private Label _yOffsetValue;
        private HSlider _spacingSlider;
        private Label _spacingValue;

        private HSlider _barHeightSlider;
        private Label _barHeightValue;
        private ColorPickerButton _barColorPicker;

        private HSlider _centerBoxHeightSlider;
        private Label _centerBoxHeightValue;
        private HSlider _centerBoxWidthScaleSlider;
        private Label _centerBoxWidthScaleValue;
        private ColorPickerButton _centerBoxColorPicker;

        public void BuildUI(VBoxContainer parent)
        {
            _visibleCheck = new CheckButton
            {
                Text = "启用铭牌背景",
                ButtonPressed = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            parent.AddChild(_visibleCheck);
            parent.AddChild(new HSeparator());

            (_yOffsetSlider, _yOffsetValue) = CreateSliderRow(parent, "垂直偏移", -200f, 200f, -80f, 1f);
            (_spacingSlider, _spacingValue) = CreateSliderRow(parent, "间距", 0f, 20f, 4f, 1f);
            (_barHeightSlider, _barHeightValue) = CreateSliderRow(parent, "顶/底栏高度", 1f, 30f, 6f, 1f);

            var barColorRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            barColorRow.AddChild(new Label { Text = "顶/底栏颜色:", CustomMinimumSize = new Vector2(100, 0) });
            _barColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(60, 26) };
            barColorRow.AddChild(_barColorPicker);
            parent.AddChild(barColorRow);

            parent.AddChild(new HSeparator());

            (_centerBoxHeightSlider, _centerBoxHeightValue) = CreateSliderRow(parent, "中块高度", 1f, 60f, 24f, 1f);
            (_centerBoxWidthScaleSlider, _centerBoxWidthScaleValue) = CreateSliderRow(parent, "中块宽度比例", 0.1f, 1.0f, 0.6f, DebugPanelLengthScalePolicy.StepF);

            var centerColorRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            centerColorRow.AddChild(new Label { Text = "中块颜色:", CustomMinimumSize = new Vector2(100, 0) });
            _centerBoxColorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(60, 26) };
            centerColorRow.AddChild(_centerBoxColorPicker);
            parent.AddChild(centerColorRow);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not NameplateData d) return;

            SetCheckSilent(_visibleCheck, d.Visible);
            SetSliderSilent(_yOffsetSlider, d.YOffset, _yOffsetValue, ((int)d.YOffset).ToString());
            SetSliderSilent(_spacingSlider, d.Spacing, _spacingValue, ((int)d.Spacing).ToString());
            SetSliderSilent(_barHeightSlider, d.BarHeight, _barHeightValue, ((int)d.BarHeight).ToString());
            _barColorPicker.Color = d.BarColor;
            SetSliderSilent(_centerBoxHeightSlider, d.CenterBoxHeight, _centerBoxHeightValue, ((int)d.CenterBoxHeight).ToString());
            SetSliderSilent(_centerBoxWidthScaleSlider, d.CenterBoxWidthScale, _centerBoxWidthScaleValue, d.CenterBoxWidthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _centerBoxColorPicker.Color = d.CenterBoxColor;
        }

        public IComponentData SyncToData()
        {
            return new NameplateData
            {
                Visible = _visibleCheck.ButtonPressed,
                YOffset = (float)_yOffsetSlider.Value,
                Spacing = (float)_spacingSlider.Value,
                BarHeight = (float)_barHeightSlider.Value,
                BarColor = _barColorPicker.Color,
                CenterBoxHeight = (float)_centerBoxHeightSlider.Value,
                CenterBoxWidthScale = EntityProfileManager.FromFp(EntityProfileManager.ToFpD(_centerBoxWidthScaleSlider.Value)),
                CenterBoxColor = _centerBoxColorPicker.Color,
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;

            _visibleCheck.Toggled += OnToggleChanged;
            _yOffsetSlider.ValueChanged += OnValueChanged;
            _spacingSlider.ValueChanged += OnValueChanged;
            _barHeightSlider.ValueChanged += OnValueChanged;
            _centerBoxHeightSlider.ValueChanged += OnValueChanged;
            _centerBoxWidthScaleSlider.ValueChanged += OnValueChanged;
            _barColorPicker.ColorChanged += OnColorChanged;
            _centerBoxColorPicker.ColorChanged += OnColorChanged;
        }

        public void DisconnectSignals()
        {
            _visibleCheck.Toggled -= OnToggleChanged;
            _yOffsetSlider.ValueChanged -= OnValueChanged;
            _spacingSlider.ValueChanged -= OnValueChanged;
            _barHeightSlider.ValueChanged -= OnValueChanged;
            _centerBoxHeightSlider.ValueChanged -= OnValueChanged;
            _centerBoxWidthScaleSlider.ValueChanged -= OnValueChanged;
            _barColorPicker.ColorChanged -= OnColorChanged;
            _centerBoxColorPicker.ColorChanged -= OnColorChanged;
        }

        public void SyncFromEntity(EntityBase entity)
        {
            if (entity == null) return;

            SetCheckSilent(_visibleCheck, entity.NameplateVisible);
            SetSliderSilent(_yOffsetSlider, entity.NameplateYOffset, _yOffsetValue, ((int)entity.NameplateYOffset).ToString());
            SetSliderSilent(_spacingSlider, entity.NameplateSpacing, _spacingValue, ((int)entity.NameplateSpacing).ToString());
            SetSliderSilent(_barHeightSlider, entity.NameplateBarHeight, _barHeightValue, ((int)entity.NameplateBarHeight).ToString());
            _barColorPicker.Color = entity.NameplateBarColor;
            SetSliderSilent(_centerBoxHeightSlider, entity.NameplateCenterBoxHeight, _centerBoxHeightValue, ((int)entity.NameplateCenterBoxHeight).ToString());
            SetSliderSilent(_centerBoxWidthScaleSlider, entity.NameplateCenterBoxWidthScale, _centerBoxWidthScaleValue, entity.NameplateCenterBoxWidthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _centerBoxColorPicker.Color = entity.NameplateCenterBoxColor;
        }

        public void SetPropertyLocked(string propertyName, bool locked)
        {
            // 当前无可锁定属性
        }

        public void SetCollapsed(bool collapsed)
        {
            // CollapsibleContainer 由 DebugPanelEntityTab 管理
        }

        public void Dispose()
        {
            DisconnectSignals();
        }

        private void OnValueChanged(double _) => _onChanged?.Invoke();
        private void OnToggleChanged(bool _) => _onChanged?.Invoke();
        private void OnColorChanged(Color _) => _onChanged?.Invoke();

        private static (HSlider slider, Label valueLabel) CreateSliderRow(
            Container parent, string label, float min, float max, float def, float step)
        {
            float actualStep = step > 0 ? step : (max <= 1.0f ? 0.05f : 1f);
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label + ":", CustomMinimumSize = new Vector2(100, 0) });

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
            var valLbl = new Label { Text = initialText, CustomMinimumSize = new Vector2(40, 0), HorizontalAlignment = HorizontalAlignment.Right };
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

        private static void SetCheckSilent(CheckButton checkButton, bool value)
        {
            checkButton.SetBlockSignals(true);
            checkButton.ButtonPressed = value;
            checkButton.SetBlockSignals(false);
        }
    }
}
