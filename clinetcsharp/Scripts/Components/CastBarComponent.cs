using Godot;
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 施法条组件 — 类似 BarGroupComponent，但读取 CastBar* 属性
    /// </summary>
    public class CastBarComponent : IEntityTabComponent
    {
        public string ComponentName => "castbar";
        public string DisplayName => "施法条";
        public Type DataType => typeof(CastBarData);

        private Action _onChanged;
        private int _gridSize = 111;

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

        private static readonly Color[] CastColors = {
            new Color(0.3f, 0.5f, 1, 1), new Color(1, 0.5f, 0, 1),
            new Color(0.8f, 0.2f, 1, 1), Colors.White,
        };

        public void BuildUI(VBoxContainer parent)
        {
            var titleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            titleRow.AddChild(new Label { Text = "施法条", CustomMinimumSize = new Vector2(45, 0) });
            _visibleCheck = new CheckButton { ButtonPressed = true };
            titleRow.AddChild(_visibleCheck);
            titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _colorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _colorBtn.Modulate = CastColors[0];
            titleRow.AddChild(_colorBtn);
            parent.AddChild(titleRow);

            _lengthValue = MakeRO(parent, "长度", "60");
            (_lengthScaleSlider, _lengthScaleValue) = MakeSR(parent, "长度比例", 0.1, 2.0, 60.0 / 111.0, DebugPanelLengthScalePolicy.Step, null, 60);
            _heightValue = MakeRO(parent, "高度", "4");
            (_heightScaleSlider, _heightScaleValue) = MakeSR(parent, "高度比例", 0.01, 0.3, 4.0 / 111.0, DebugPanelLengthScalePolicy.Step, null, 60);
            (_fillSlider, _fillValue) = MakeSR(parent, "填充", 0, 100, 0, 1, v => $"{(int)v}%", 35);
            _offsetXCenterCheck = new CheckButton { Text = "居中", ButtonPressed = true };
            (_offsetXSlider, _offsetXValue) = MakeSR(parent, "X偏移", -150, 150, 0, 1, v => ((int)v).ToString(), 45, _offsetXCenterCheck);
            (_offsetYSlider, _offsetYValue) = MakeSR(parent, "Y偏移", -150, 150, -80, 1, v => ((int)v).ToString(), 45);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not CastBarData d) return;
            SetCheckSilent(_visibleCheck, d.Visible);
            _colorBtn.Modulate = d.Color;
            SetSliderSilent(_lengthScaleSlider, d.LengthScale, _lengthScaleValue, d.LengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _lengthValue.Text = ((int)(_gridSize * d.LengthScale)).ToString();
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
            return new CastBarData
            {
                Visible = _visibleCheck.ButtonPressed,
                Color = _colorBtn.Modulate,
                LengthScale = (float)_lengthScaleSlider.Value,
                HeightScale = (float)_heightScaleSlider.Value,
                FillPercent = (float)(_fillSlider.Value / 100.0),
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
            SetCheckSilent(_visibleCheck, entity.CastBarVisible);
            _colorBtn.Modulate = entity.CastBarColor;
            SetSliderSilent(_lengthScaleSlider, entity.CastBarLengthScale, _lengthScaleValue, entity.CastBarLengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _lengthValue.Text = ((int)(_gridSize * entity.CastBarLengthScale)).ToString();
            SetSliderSilent(_heightScaleSlider, entity.CastBarHeightScale, _heightScaleValue, entity.CastBarHeightScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _heightValue.Text = ((int)(_gridSize * entity.CastBarHeightScale)).ToString();
            SetSliderSilent(_fillSlider, entity.CastBarFillPercent * 100, _fillValue, $"{(int)(entity.CastBarFillPercent * 100)}%");
            SetSliderSilent(_offsetXSlider, entity.CastBarOffset.X, _offsetXValue, ((int)entity.CastBarOffset.X).ToString());
            SetCheckSilent(_offsetXCenterCheck, entity.CastBarCenterX);
            _offsetXSlider.Editable = !entity.CastBarCenterX;
            _offsetXSlider.Modulate = entity.CastBarCenterX ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
            SetSliderSilent(_offsetYSlider, entity.CastBarOffset.Y, _offsetYValue, ((int)entity.CastBarOffset.Y).ToString());
        }

        public void SetPropertyLocked(string propertyName, bool locked)
        {
            if (locked) _lockedProperties.Add(propertyName); else _lockedProperties.Remove(propertyName);
            ApplyLocks();
        }

        private void ApplyLocks()
        {
            if (_lockedProperties.Contains("fill_percent"))
            { _fillSlider.Editable = false; _fillSlider.Modulate = new Color(0.5f, 0.5f, 0.5f, 1); }
            else { _fillSlider.Editable = true; _fillSlider.Modulate = new Color(1, 1, 1, 1); }
        }

        public void SetCollapsed(bool collapsed) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }
        public void Dispose() { DisconnectSignals(); }

        void OnDbl(double _) => _onChanged?.Invoke();
        void OnBool(bool _) => _onChanged?.Invoke();

        void OnColorPressed()
        {
            var cur = _colorBtn.Modulate; int next = 0;
            for (int i = 0; i < CastColors.Length; i++) if (cur.IsEqualApprox(CastColors[i])) { next = (i + 1) % CastColors.Length; break; }
            _colorBtn.Modulate = CastColors[next]; _onChanged?.Invoke();
        }

        void OnCenterXToggled(bool centered)
        {
            _offsetXSlider.Editable = !centered;
            _offsetXSlider.Modulate = centered ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
            if (centered) { _offsetXSlider.SetBlockSignals(true); _offsetXSlider.Value = 0; _offsetXSlider.SetBlockSignals(false); _offsetXValue.Text = "0"; }
            _onChanged?.Invoke();
        }

        static Label MakeRO(Container p, string lbl, string init)
        {
            var r = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            r.AddChild(new Label { Text = lbl, CustomMinimumSize = new Vector2(60, 0) });
            var v = new Label { Text = init, CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            r.AddChild(v); p.AddChild(r); return v;
        }

        static (HSlider, Label) MakeSR(Container p, string lbl, double min, double max, double def, double step, Func<double, string> fmt = null, int lw = 60, CheckButton cc = null)
        {
            fmt ??= (v => v.ToString(DebugPanelLengthScalePolicy.FormatStr));
            var r = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            r.AddChild(new Label { Text = lbl, CustomMinimumSize = new Vector2(lw, 0) });
            var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = def, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            r.AddChild(s);
            var v = new Label { Text = fmt(def), CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            r.AddChild(v);
            if (cc != null) r.AddChild(cc);
            s.ValueChanged += (val) => v.Text = fmt(val);
            p.AddChild(r);
            SliderValueInput.Attach(s, v, fmt);
            return (s, v);
        }

        static void SetSliderSilent(HSlider s, double v, Label l, string t)
        { s?.SetBlockSignals(true); if (s != null) s.Value = v; s?.SetBlockSignals(false); if (l != null) l.Text = t; }

        static void SetCheckSilent(CheckButton c, bool v)
        { c?.SetBlockSignals(true); if (c != null) c.ButtonPressed = v; c?.SetBlockSignals(false); }
    }
}