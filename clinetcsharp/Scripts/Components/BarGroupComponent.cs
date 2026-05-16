using Godot;
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 条形组组件 — 血条/MP条通用，参数化构造
    /// </summary>
    public class BarGroupComponent : IEntityTabComponent
    {
        public string ComponentName => _componentName;
        public string DisplayName => _displayName;
        public Type DataType => typeof(BarData);

        private readonly string _componentName;
        private readonly string _displayName;
        private Action _onChanged;
        private int _gridSize = 111;
        private float _lengthBaseSize = 111f;

        private CheckButton _visibleCheck;
        private Button _colorBtn;
        private Label _lengthValue;
        private HSlider _lengthScaleSlider;
        private Label _lengthScaleValue;
        private Label _heightValue;
        private HSlider _heightScaleSlider;
        private Label _heightScaleValue;
        private HSlider _fillSlider;
        private Label _fillValue;
        private HSlider _offsetXSlider;
        private Label _offsetXValue;
        private CheckButton _offsetXCenterCheck;
        private HSlider _offsetYSlider;
        private Label _offsetYValue;

        private HashSet<string> _lockedProperties = new();

        private readonly Color[] _colorPalette;

        private static readonly Color[] HpColors = {
            new Color(0, 0.8f, 0, 1), new Color(1, 0.2f, 0.2f, 1),
            new Color(1, 0.8f, 0, 1), new Color(0.5f, 0.5f, 0.5f, 1),
        };
        private static readonly Color[] MpColors = {
            new Color(0.2f, 0.4f, 1.0f, 1), new Color(0.6f, 0.2f, 0.8f, 1),
            new Color(0.2f, 0.8f, 0.8f, 1), new Color(0.8f, 0.4f, 0.2f, 1),
        };

        public BarGroupComponent(string componentName, string displayName)
        {
            _componentName = componentName;
            _displayName = displayName;
            _colorPalette = componentName == "mpbar" ? MpColors : HpColors;
        }

        public void BuildUI(VBoxContainer parent)
        {
            // Title row: name [visible] [color]
            var titleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            titleRow.AddChild(new Label { Text = DisplayName, CustomMinimumSize = new Vector2(45, 0) });
            _visibleCheck = new CheckButton { ButtonPressed = true };
            titleRow.AddChild(_visibleCheck);
            titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _colorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _colorBtn.Modulate = _colorPalette[0];
            titleRow.AddChild(_colorBtn);
            parent.AddChild(titleRow);

            // Length (read-only) + LengthScale
            _lengthValue = MakeReadOnlyRow(parent, "长度", "102");
            (_lengthScaleSlider, _lengthScaleValue) = MakeSliderRow(parent, "长度比例", 0.1, 2.0,
                _componentName == "mpbar" ? 80.0 / 111.0 : 102.0 / 111.0,
                DebugPanelLengthScalePolicy.Step, null, 60);
            // Height (read-only) + HeightScale
            _heightValue = MakeReadOnlyRow(parent, "高度", _componentName == "mpbar" ? "4" : "6");
            (_heightScaleSlider, _heightScaleValue) = MakeSliderRow(parent, "高度比例", 0.01, 0.3,
                _componentName == "mpbar" ? 4.0 / 111.0 : 6.0 / 111.0,
                DebugPanelLengthScalePolicy.Step, null, 60);
            // Fill
            (_fillSlider, _fillValue) = MakeSliderRow(parent, "填充", 0, 100, 100, 1, v => $"{(int)v}%", 35);
            // X offset + center
            _offsetXCenterCheck = new CheckButton { Text = "居中", ButtonPressed = true };
            (_offsetXSlider, _offsetXValue) = MakeSliderRow(parent, "X偏移", -150, 150, 0, 1,
                v => ((int)v).ToString(), 45, _offsetXCenterCheck);
            // Y offset
            (_offsetYSlider, _offsetYValue) = MakeSliderRow(parent, "Y偏移", -150, 150,
                _componentName == "mpbar" ? -62 : -70, 1, v => ((int)v).ToString(), 45);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not BarData d) return;
            SetCheckSilent(_visibleCheck, d.Visible);
            _colorBtn.Modulate = d.Color;
            SetSliderSilent(_lengthScaleSlider, d.LengthScale, _lengthScaleValue, d.LengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _lengthValue.Text = ((int)(_lengthBaseSize * d.LengthScale)).ToString();
            SetSliderSilent(_heightScaleSlider, d.HeightScale, _heightScaleValue, d.HeightScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _heightValue.Text = ((int)(_gridSize * d.HeightScale)).ToString();
            SetSliderSilent(_fillSlider, d.FillPercent * 100, _fillValue, $"{(int)(d.FillPercent * 100)}%");
            SetSliderSilent(_offsetXSlider, d.OffsetX, _offsetXValue, ((int)d.OffsetX).ToString());
            SetCheckSilent(_offsetXCenterCheck, d.CenterX);
            _offsetXSlider.Editable = !d.CenterX;
            _offsetXSlider.Modulate = d.CenterX ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
            SetSliderSilent(_offsetYSlider, d.OffsetY, _offsetYValue, ((int)d.OffsetY).ToString());
            _lockedProperties = new HashSet<string>(d.LockedProperties);
            ApplyLocks();
        }

        public IComponentData SyncToData()
        {
            return new BarData
            {
                Visible = _visibleCheck.ButtonPressed,
                Color = _colorBtn.Modulate,
                LengthScale = EntityProfileManager.FromFp(EntityProfileManager.ToFpD(_lengthScaleSlider.Value)),
                HeightScale = EntityProfileManager.FromFp(EntityProfileManager.ToFpD(_heightScaleSlider.Value)),
                FillPercent = EntityProfileManager.FromFp(EntityProfileManager.ToFpD(_fillSlider.Value / 100.0)),
                CenterX = _offsetXCenterCheck.ButtonPressed,
                OffsetX = _offsetXCenterCheck.ButtonPressed ? 0 : (float)_offsetXSlider.Value,
                OffsetY = (float)_offsetYSlider.Value,
                LockedProperties = new HashSet<string>(_lockedProperties),
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;
            _visibleCheck.Toggled += OnBool;
            _colorBtn.Pressed += OnColorPressed;
            _lengthScaleSlider.ValueChanged += OnDbl;
            _heightScaleSlider.ValueChanged += OnDbl;
            _fillSlider.ValueChanged += OnDbl;
            _offsetXSlider.ValueChanged += OnDbl;
            _offsetXCenterCheck.Toggled += OnCenterXToggled;
            _offsetYSlider.ValueChanged += OnDbl;
        }

        public void DisconnectSignals()
        {
            _visibleCheck.Toggled -= OnBool;
            _colorBtn.Pressed -= OnColorPressed;
            _lengthScaleSlider.ValueChanged -= OnDbl;
            _heightScaleSlider.ValueChanged -= OnDbl;
            _fillSlider.ValueChanged -= OnDbl;
            _offsetXSlider.ValueChanged -= OnDbl;
            _offsetXCenterCheck.Toggled -= OnCenterXToggled;
            _offsetYSlider.ValueChanged -= OnDbl;
        }

        public void SyncFromEntity(EntityBase entity)
        {
            if (entity == null) return;
            _gridSize = entity.GridSize;
            _lengthBaseSize = entity.VisualOuterSize;

            if (_componentName == "healthbar")
            {
                SetCheckSilent(_visibleCheck, entity.HealthBarVisible);
                _colorBtn.Modulate = entity.HealthBarColor;
                SetSliderSilent(_lengthScaleSlider, entity.HealthBarLengthScale, _lengthScaleValue, entity.HealthBarLengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
                _lengthValue.Text = ((int)entity.HealthBarLength).ToString();
                SetSliderSilent(_heightScaleSlider, entity.HealthBarHeightScale, _heightScaleValue, entity.HealthBarHeightScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
                _heightValue.Text = ((int)(_gridSize * entity.HealthBarHeightScale)).ToString();
                SetSliderSilent(_fillSlider, entity.HealthBarFillPercent * 100, _fillValue, $"{(int)(entity.HealthBarFillPercent * 100)}%");
                SetSliderSilent(_offsetXSlider, entity.HealthBarOffset.X, _offsetXValue, ((int)entity.HealthBarOffset.X).ToString());
                SetCheckSilent(_offsetXCenterCheck, entity.HealthBarCenterX);
                _offsetXSlider.Editable = !entity.HealthBarCenterX;
                _offsetXSlider.Modulate = entity.HealthBarCenterX ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                SetSliderSilent(_offsetYSlider, entity.HealthBarOffset.Y, _offsetYValue, ((int)entity.HealthBarOffset.Y).ToString());
            }
            else if (_componentName == "mpbar")
            {
                SetCheckSilent(_visibleCheck, entity.MpBarVisible);
                _colorBtn.Modulate = entity.MpBarColor;
                SetSliderSilent(_lengthScaleSlider, entity.MpBarLengthScale, _lengthScaleValue, entity.MpBarLengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
                _lengthValue.Text = ((int)entity.MpBarLength).ToString();
                SetSliderSilent(_heightScaleSlider, entity.MpBarHeightScale, _heightScaleValue, entity.MpBarHeightScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
                _heightValue.Text = ((int)(_gridSize * entity.MpBarHeightScale)).ToString();
                SetSliderSilent(_fillSlider, entity.MpBarFillPercent * 100, _fillValue, $"{(int)(entity.MpBarFillPercent * 100)}%");
                SetSliderSilent(_offsetXSlider, entity.MpBarOffset.X, _offsetXValue, ((int)entity.MpBarOffset.X).ToString());
                SetCheckSilent(_offsetXCenterCheck, entity.MpBarCenterX);
                _offsetXSlider.Editable = !entity.MpBarCenterX;
                _offsetXSlider.Modulate = entity.MpBarCenterX ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                SetSliderSilent(_offsetYSlider, entity.MpBarOffset.Y, _offsetYValue, ((int)entity.MpBarOffset.Y).ToString());
            }
        }

        public void SetPropertyLocked(string propertyName, bool locked)
        {
            if (locked) _lockedProperties.Add(propertyName); else _lockedProperties.Remove(propertyName);
            ApplyLocks();
        }

        private void ApplyLocks()
        {
            if (_lockedProperties.Contains("fill_percent"))
            {
                _fillSlider.Editable = false;
                _fillSlider.Modulate = new Color(0.5f, 0.5f, 0.5f, 1);
            }
            else
            {
                _fillSlider.Editable = true;
                _fillSlider.Modulate = new Color(1, 1, 1, 1);
            }
        }

        public void SetCollapsed(bool collapsed) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }
        public void Dispose() { DisconnectSignals(); }

        private void OnDbl(double _) => _onChanged?.Invoke();
        private void OnBool(bool _) => _onChanged?.Invoke();

        private void OnColorPressed()
        {
            var cur = _colorBtn.Modulate; int next = 0;
            for (int i = 0; i < _colorPalette.Length; i++) if (cur.IsEqualApprox(_colorPalette[i])) { next = (i + 1) % _colorPalette.Length; break; }
            _colorBtn.Modulate = _colorPalette[next]; _onChanged?.Invoke();
        }

        private void OnCenterXToggled(bool centered)
        {
            _offsetXSlider.Editable = !centered;
            _offsetXSlider.Modulate = centered ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
            if (centered) { _offsetXSlider.SetBlockSignals(true); _offsetXSlider.Value = 0; _offsetXSlider.SetBlockSignals(false); _offsetXValue.Text = "0"; }
            _onChanged?.Invoke();
        }

        #region UI Helpers
        private static Label MakeReadOnlyRow(Container parent, string label, string initial)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(60, 0) });
            var v = new Label { Text = initial, CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            row.AddChild(v); parent.AddChild(row); return v;
        }

        private static (HSlider, Label) MakeSliderRow(Container parent, string label, double min, double max,
            double def, double step, Func<double, string> fmt = null, int lw = 60, CheckButton centerCheck = null)
        {
            fmt ??= (v => v.ToString(DebugPanelLengthScalePolicy.FormatStr));
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(lw, 0) });
            var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = def, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            row.AddChild(s);
            var v = new Label { Text = fmt(def), CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            row.AddChild(v);
            if (centerCheck != null) row.AddChild(centerCheck);
            s.ValueChanged += (val) => v.Text = fmt(val);
            parent.AddChild(row);
            SliderValueInput.Attach(s, v, fmt);
            return (s, v);
        }

        private static void SetSliderSilent(HSlider s, double v, Label l, string t)
        { s?.SetBlockSignals(true); if (s != null) s.Value = v; s?.SetBlockSignals(false); if (l != null) l.Text = t; }

        private static void SetCheckSilent(CheckButton c, bool v)
        { c?.SetBlockSignals(true); if (c != null) c.ButtonPressed = v; c?.SetBlockSignals(false); }
        #endregion
    }
}
