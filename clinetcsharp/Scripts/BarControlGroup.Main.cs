using Godot;
using System;

namespace ClinetCSharp
{
    public partial class BarControlGroup
    {
        #region Config Key Constants
        public const string KeyVisible = "visible";
        public const string KeyLength = "length";
        public const string KeyLengthScale = "length_scale";
        public const string KeyHeight = "height";
        public const string KeyHeightScale = "height_scale";
        public const string KeyFill = "fill";
        public const string KeyOffsetX = "offset_x";
        public const string KeyOffsetY = "offset_y";
        public const string KeyCenterX = "center_x";
        public const string KeyColorR = "color_r";
        public const string KeyColorG = "color_g";
        public const string KeyColorB = "color_b";
        #endregion

        #region Fields
        private readonly DebugPanelTab _owner;
        private readonly string _configSection;
        private readonly string _displayName;
        private readonly Color[] _colorPalette;
        private readonly BarControlGroupDefaults _defaults;
        private readonly IBarEntityAdapter _adapter;

        private CheckButton _visibleCheck;
        private Button _colorBtn;
        private HSlider _lengthSlider;
        private HSlider _lengthScaleSlider;
        private HSlider _heightSlider;
        private HSlider _heightScaleSlider;
        private HSlider _fillSlider;
        private HSlider _offsetXSlider;
        private CheckButton _offsetXCenterCheck;
        private HSlider _offsetYSlider;
        private Label _computedLengthLabel;
        private Label _computedHeightLabel;
        #endregion

        #region Constructor
        public BarControlGroup(
            DebugPanelTab owner, string configSection, string displayName,
            IBarEntityAdapter adapter, Color[] colorPalette, BarControlGroupDefaults defaults)
        {
            _owner = owner;
            _configSection = configSection;
            _displayName = displayName;
            _adapter = adapter;
            _colorPalette = colorPalette;
            _defaults = defaults;
        }
        #endregion

        #region BuildUI
        public void BuildUI(Container parent)
        {
            parent.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            // Title row: name [visible] [color]
            var titleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            titleRow.AddChild(new Label { Text = _displayName, CustomMinimumSize = new Vector2(45, 0) });
            _visibleCheck = new CheckButton { ButtonPressed = _defaults.Visible };
            titleRow.AddChild(_visibleCheck);
            titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _colorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _colorBtn.Modulate = _defaults.Color;
            titleRow.AddChild(_colorBtn);
            parent.AddChild(titleRow);

            _lengthSlider = MakeSliderRow(parent, "长度", 20, 400, _defaults.Length, 1,
                v => ((int)v).ToString(), 35);
            _lengthScaleSlider = MakeSliderRow(parent, "长度比例", 0.1, 2.0, _defaults.LengthScale,
                DebugPanelLengthScalePolicy.Step, null, 60);
            _computedLengthLabel = MakeReadOnlyRow(parent, "  实际长度",
                ((int)(_defaults.GridSize * _defaults.LengthScale)).ToString());
            _heightSlider = MakeSliderRow(parent, "高度", 2, 40, _defaults.Height, 1,
                v => ((int)v).ToString(), 35);
            _heightScaleSlider = MakeSliderRow(parent, "高度比例", 0.01, 1.0, _defaults.HeightScale,
                DebugPanelLengthScalePolicy.Step, null, 60);
            _computedHeightLabel = MakeReadOnlyRow(parent, "  实际高度",
                ((int)(_defaults.GridSize * _defaults.HeightScale)).ToString());
            _fillSlider = MakeSliderRow(parent, "填充%", 0, 100, _defaults.Fill, 1,
                v => $"{(int)v}%", 35);

            // X offset + center check
            var oxRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            oxRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _offsetXCenterCheck = new CheckButton { Text = "居中", ButtonPressed = _defaults.CenterX };
            oxRow.AddChild(_offsetXCenterCheck);
            _offsetXSlider = new HSlider
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MinValue = -150,
                MaxValue = 150,
                Step = 1,
                Value = _defaults.OffsetX,
                Scrollable = false,
                Editable = !_defaults.CenterX,
                FocusMode = Control.FocusModeEnum.Click
            };
            if (_defaults.CenterX) _offsetXSlider.Modulate = new Color(0.5f, 0.5f, 0.5f, 1);
            oxRow.AddChild(_offsetXSlider);
            parent.AddChild(oxRow);
            _owner.AttachValueLineEdit(_offsetXSlider,
                new Label { Text = ((int)_defaults.OffsetX).ToString(), CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right },
                v => ((int)v).ToString());

            _offsetYSlider = MakeSliderRow(parent, "Y偏移", -150, 150, _defaults.OffsetY, 1,
                v => ((int)v).ToString(), 45);
        }

        private HSlider MakeSliderRow(Container parent, string label, double min, double max,
            double def, double step, Func<double, string> formatValue, int labelWidth)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(labelWidth, 0) });
            var slider = new HSlider
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MinValue = min,
                MaxValue = max,
                Step = step,
                Value = def,
                Scrollable = false,
                FocusMode = Control.FocusModeEnum.Click
            };
            row.AddChild(slider);
            var valLbl = new Label
            {
                Text = formatValue != null ? formatValue(def) : def.ToString(DebugPanelLengthScalePolicy.FormatStr),
                CustomMinimumSize = new Vector2(30, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            row.AddChild(valLbl);
            parent.AddChild(row);
            _owner.AttachValueLineEdit(slider, valLbl, formatValue);
            return slider;
        }

        private static Label MakeReadOnlyRow(Container parent, string label, string initialText)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(70, 0) });
            var valLbl = new Label
            {
                Text = initialText,
                CustomMinimumSize = new Vector2(30, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            row.AddChild(valLbl);
            parent.AddChild(row);
            return valLbl;
        }
        #endregion

        #region ConnectSignals / DisconnectSignals
        public void ConnectSignals()
        {
            _visibleCheck.Toggled += OnVisibleToggled;
            _colorBtn.Pressed += OnColorPressed;
            _lengthSlider.ValueChanged += OnLengthChanged;
            _lengthScaleSlider.ValueChanged += OnLengthScaleChanged;
            _heightSlider.ValueChanged += OnHeightChanged;
            _heightScaleSlider.ValueChanged += OnHeightScaleChanged;
            _fillSlider.ValueChanged += OnFillChanged;
            _offsetXSlider.ValueChanged += OnOffsetXChanged;
            _offsetXCenterCheck.Toggled += OnOffsetXCenterToggled;
            _offsetYSlider.ValueChanged += OnOffsetYChanged;
        }

        public void DisconnectSignals()
        {
            _visibleCheck.Toggled -= OnVisibleToggled;
            _colorBtn.Pressed -= OnColorPressed;
            _lengthSlider.ValueChanged -= OnLengthChanged;
            _lengthScaleSlider.ValueChanged -= OnLengthScaleChanged;
            _heightSlider.ValueChanged -= OnHeightChanged;
            _heightScaleSlider.ValueChanged -= OnHeightScaleChanged;
            _fillSlider.ValueChanged -= OnFillChanged;
            _offsetXSlider.ValueChanged -= OnOffsetXChanged;
            _offsetXCenterCheck.Toggled -= OnOffsetXCenterToggled;
            _offsetYSlider.ValueChanged -= OnOffsetYChanged;
        }
        #endregion

        #region Handlers
        private void OnVisibleToggled(bool enabled)
        {
            _adapter.SetVisible(enabled);
        }

        private void OnColorPressed()
        {
            var currentColor = _adapter.GetColor();
            int idx = 0;
            for (int i = 0; i < _colorPalette.Length; i++)
            {
                if (currentColor.IsEqualApprox(_colorPalette[i])) { idx = i; break; }
            }
            int nextIdx = (idx + 1) % _colorPalette.Length;
            _adapter.SetColor(_colorPalette[nextIdx]);
            _colorBtn.Modulate = _colorPalette[nextIdx];
        }

        private void OnLengthChanged(double value)
        {
            int gridSize = _adapter.GetGridSize();
            if (gridSize > 0)
            {
                float scale = (float)(value / gridSize);
                _adapter.SetLengthScale(scale);
                _lengthScaleSlider.SetBlockSignals(true);
                _lengthScaleSlider.Value = scale;
                _lengthScaleSlider.SetBlockSignals(false);
                _owner.UpdateAttachedValue(_lengthScaleSlider, scale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            }
            UpdateComputedLength();
        }

        private void OnLengthScaleChanged(double value)
        {
            int gridSize = _adapter.GetGridSize();
            _adapter.SetLengthScale((float)value);
            if (gridSize > 0)
            {
                int newLength = (int)(gridSize * value);
                _lengthSlider.SetBlockSignals(true);
                _lengthSlider.Value = newLength;
                _lengthSlider.SetBlockSignals(false);
                _owner.UpdateAttachedValue(_lengthSlider, newLength.ToString());
            }
            UpdateComputedLength();
        }

        private void OnHeightChanged(double value)
        {
            int gridSize = _adapter.GetGridSize();
            if (gridSize > 0)
            {
                float scale = (float)(value / gridSize);
                _adapter.SetHeightScale(scale);
                _heightScaleSlider.SetBlockSignals(true);
                _heightScaleSlider.Value = scale;
                _heightScaleSlider.SetBlockSignals(false);
                _owner.UpdateAttachedValue(_heightScaleSlider, scale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            }
            UpdateComputedHeight();
        }

        private void OnHeightScaleChanged(double value)
        {
            int gridSize = _adapter.GetGridSize();
            _adapter.SetHeightScale((float)value);
            if (gridSize > 0)
            {
                int newHeight = (int)(gridSize * value);
                _heightSlider.SetBlockSignals(true);
                _heightSlider.Value = newHeight;
                _heightSlider.SetBlockSignals(false);
                _owner.UpdateAttachedValue(_heightSlider, newHeight.ToString());
            }
            UpdateComputedHeight();
        }

        private void OnFillChanged(double value)
        {
            _adapter.SetFillPercent((float)(value / 100.0));
        }

        private void OnOffsetXChanged(double value)
        {
            var offset = _adapter.GetOffset();
            _adapter.SetOffset(new Vector2((float)value, offset.Y));
        }

        private void OnOffsetXCenterToggled(bool centered)
        {
            _offsetXSlider.Editable = !centered;
            _offsetXSlider.Modulate = centered ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
            if (centered)
            {
                _offsetXSlider.SetBlockSignals(true);
                _offsetXSlider.Value = 0;
                _offsetXSlider.SetBlockSignals(false);
                _owner.UpdateAttachedValue(_offsetXSlider, "0");
                var offset = _adapter.GetOffset();
                _adapter.SetOffset(new Vector2(0, offset.Y));
            }
        }

        private void OnOffsetYChanged(double value)
        {
            var offset = _adapter.GetOffset();
            _adapter.SetOffset(new Vector2(offset.X, (float)value));
        }

        private void UpdateComputedLength()
        {
            int gridSize = _adapter.GetGridSize();
            if (_computedLengthLabel != null && gridSize > 0)
                _computedLengthLabel.Text = ((int)(gridSize * _lengthScaleSlider.Value)).ToString();
        }

        private void UpdateComputedHeight()
        {
            int gridSize = _adapter.GetGridSize();
            if (_computedHeightLabel != null && gridSize > 0)
                _computedHeightLabel.Text = ((int)(gridSize * _heightScaleSlider.Value)).ToString();
        }
        #endregion

        #region SaveConfig / LoadConfig
        public void SaveConfig(ConfigFile cfg)
        {
            cfg.SetValue(_configSection, KeyVisible, _visibleCheck?.ButtonPressed ?? _defaults.Visible);
            cfg.SetValue(_configSection, KeyLength, _lengthSlider?.Value ?? _defaults.Length);
            cfg.SetValue(_configSection, KeyLengthScale, _lengthScaleSlider?.Value ?? _defaults.LengthScale);
            cfg.SetValue(_configSection, KeyHeight, _heightSlider?.Value ?? _defaults.Height);
            cfg.SetValue(_configSection, KeyHeightScale, _heightScaleSlider?.Value ?? _defaults.HeightScale);
            cfg.SetValue(_configSection, KeyFill, _fillSlider?.Value ?? _defaults.Fill);
            var offset = _adapter.GetOffset();
            cfg.SetValue(_configSection, KeyOffsetX, (double)offset.X);
            cfg.SetValue(_configSection, KeyOffsetY, (double)offset.Y);
            cfg.SetValue(_configSection, KeyCenterX, _offsetXCenterCheck?.ButtonPressed ?? _defaults.CenterX);
            var color = _adapter.GetColor();
            cfg.SetValue(_configSection, KeyColorR, color.R);
            cfg.SetValue(_configSection, KeyColorG, color.G);
            cfg.SetValue(_configSection, KeyColorB, color.B);
        }

        public void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            if (!configLoaded) return;

            bool visible = (bool)cfg.GetValue(_configSection, KeyVisible, _defaults.Visible);
            double length = (double)cfg.GetValue(_configSection, KeyLength, _defaults.Length);
            double lengthScale = (double)cfg.GetValue(_configSection, KeyLengthScale, _defaults.LengthScale);
            double height = (double)cfg.GetValue(_configSection, KeyHeight, _defaults.Height);
            double heightScale = (double)cfg.GetValue(_configSection, KeyHeightScale, _defaults.HeightScale);
            double fill = (double)cfg.GetValue(_configSection, KeyFill, _defaults.Fill);
            double offX = (double)cfg.GetValue(_configSection, KeyOffsetX, _defaults.OffsetX);
            double offY = (double)cfg.GetValue(_configSection, KeyOffsetY, _defaults.OffsetY);
            bool centerX = (bool)cfg.GetValue(_configSection, KeyCenterX, _defaults.CenterX);
            float cr = (float)(double)cfg.GetValue(_configSection, KeyColorR, _defaults.Color.R);
            float cg = (float)(double)cfg.GetValue(_configSection, KeyColorG, _defaults.Color.G);
            float cb = (float)(double)cfg.GetValue(_configSection, KeyColorB, _defaults.Color.B);

            // Apply to entity
            _adapter.SetVisible(visible);
            _adapter.SetLengthScale((float)lengthScale);
            _adapter.SetHeightScale((float)heightScale);
            _adapter.SetFillPercent((float)(fill / 100.0));
            _adapter.SetOffset(new Vector2((float)offX, (float)offY));
            _adapter.SetColor(new Color(cr, cg, cb));

            // Sync UI
            SetControlSilent(_visibleCheck, visible);
            SetControlSilent(_lengthSlider, length);
            SetControlSilent(_lengthScaleSlider, lengthScale);
            SetControlSilent(_heightSlider, height);
            SetControlSilent(_heightScaleSlider, heightScale);
            SetControlSilent(_fillSlider, fill);
            SetControlSilent(_offsetXSlider, offX);
            SetControlSilent(_offsetYSlider, offY);

            _owner.UpdateAttachedValue(_lengthSlider, ((int)length).ToString());
            _owner.UpdateAttachedValue(_lengthScaleSlider, lengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _owner.UpdateAttachedValue(_heightSlider, ((int)height).ToString());
            _owner.UpdateAttachedValue(_heightScaleSlider, heightScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _owner.UpdateAttachedValue(_fillSlider, $"{(int)fill}%");
            _owner.UpdateAttachedValue(_offsetXSlider, ((int)offX).ToString());
            _owner.UpdateAttachedValue(_offsetYSlider, ((int)offY).ToString());

            SetControlSilent(_offsetXCenterCheck, centerX);
            _offsetXSlider.Editable = !centerX;
            _offsetXSlider.Modulate = centerX ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
            _colorBtn.Modulate = new Color(cr, cg, cb);

            UpdateComputedLength();
            UpdateComputedHeight();
        }

        private static void SetControlSilent(CheckButton check, bool value)
        {
            if (check != null) { check.SetBlockSignals(true); check.ButtonPressed = value; check.SetBlockSignals(false); }
        }

        private static void SetControlSilent(HSlider slider, double value)
        {
            if (slider != null) { slider.SetBlockSignals(true); slider.Value = value; slider.SetBlockSignals(false); }
        }
        #endregion
    }
}
