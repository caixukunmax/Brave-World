using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 等级徽章组件 — 可见性、字号、颜色、内容、偏移
    /// </summary>
    public class LevelBadgeComponent : IEntityTabComponent
    {
        public string ComponentName => "levelbadge";
        public string DisplayName => "等级徽章";
        public Type DataType => typeof(LevelBadgeData);

        private Action _onChanged;

        private CheckButton _visibleCheck;
        private HSlider _fontSizeSlider;
        private Label _fontSizeValue;
        private Button _textColorBtn;
        private LineEdit _textEdit;
        private HSlider _offsetXSlider;
        private Label _offsetXValue;
        private CheckButton _offsetXCenterCheck;
        private HSlider _offsetYSlider;
        private Label _offsetYValue;

        private static readonly Color[] BadgeColors = {
            Colors.Yellow, Colors.Red, Colors.Blue, Colors.Green, Colors.White,
        };

        public void BuildUI(VBoxContainer parent)
        {
            // Title row
            var titleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            titleRow.AddChild(new Label { Text = "等级", CustomMinimumSize = new Vector2(45, 0) });
            _visibleCheck = new CheckButton { ButtonPressed = true };
            titleRow.AddChild(_visibleCheck);
            titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _textColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _textColorBtn.Modulate = Colors.Yellow;
            titleRow.AddChild(_textColorBtn);
            parent.AddChild(titleRow);

            // Text content
            var txtRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            txtRow.AddChild(new Label { Text = "内容", CustomMinimumSize = new Vector2(35, 0) });
            _textEdit = new LineEdit { Text = "Lv.{level}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0), PlaceholderText = "可用: {level} {name} {job}" };
            txtRow.AddChild(_textEdit);
            parent.AddChild(txtRow);

            // Font size
            var fsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fsRow.AddChild(new Label { Text = "字号", CustomMinimumSize = new Vector2(35, 0) });
            _fontSizeSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = 6, MaxValue = 24, Step = 1, Value = 12, Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            fsRow.AddChild(_fontSizeSlider);
            _fontSizeValue = new Label { Text = "12", CustomMinimumSize = new Vector2(25, 0), HorizontalAlignment = HorizontalAlignment.Right };
            fsRow.AddChild(_fontSizeValue);
            SliderValueInput.Attach(_fontSizeSlider, _fontSizeValue, v => ((int)v).ToString());
            parent.AddChild(fsRow);

            // X offset + center
            var oxRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            oxRow.AddChild(new Label { Text = "X偏移", CustomMinimumSize = new Vector2(45, 0) });
            _offsetXCenterCheck = new CheckButton { Text = "居中", ButtonPressed = false };
            oxRow.AddChild(_offsetXCenterCheck);
            _offsetXSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -35, Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            oxRow.AddChild(_offsetXSlider);
            _offsetXValue = new Label { Text = "-35", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            oxRow.AddChild(_offsetXValue);
            SliderValueInput.Attach(_offsetXSlider, _offsetXValue, v => ((int)v).ToString());
            parent.AddChild(oxRow);

            // Y offset
            var oyRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            oyRow.AddChild(new Label { Text = "Y偏移", CustomMinimumSize = new Vector2(45, 0) });
            _offsetYSlider = new HSlider { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MinValue = -150, MaxValue = 150, Step = 1, Value = -35, Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            oyRow.AddChild(_offsetYSlider);
            _offsetYValue = new Label { Text = "-35", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            oyRow.AddChild(_offsetYValue);
            SliderValueInput.Attach(_offsetYSlider, _offsetYValue, v => ((int)v).ToString());
            parent.AddChild(oyRow);

            // Center X linkage
            _offsetXCenterCheck.Toggled += (centered) =>
            {
                _offsetXSlider.Editable = !centered;
                _offsetXSlider.Modulate = centered ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
                if (centered) { _offsetXSlider.SetBlockSignals(true); _offsetXSlider.Value = 0; _offsetXSlider.SetBlockSignals(false); _offsetXValue.Text = "0"; }
            };
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not LevelBadgeData d) return;
            SetCheckSilent(_visibleCheck, d.Visible);
            _textEdit.Text = d.Text ?? "";
            SetSliderSilent(_fontSizeSlider, d.FontSize, _fontSizeValue, d.FontSize.ToString("F0"));
            _textColorBtn.Modulate = d.TextColor;
            SetSliderSilent(_offsetXSlider, d.OffsetX, _offsetXValue, ((int)d.OffsetX).ToString());
            SetCheckSilent(_offsetXCenterCheck, d.CenterX);
            _offsetXSlider.Editable = !d.CenterX;
            _offsetXSlider.Modulate = d.CenterX ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
            SetSliderSilent(_offsetYSlider, d.OffsetY, _offsetYValue, ((int)d.OffsetY).ToString());
        }

        public IComponentData SyncToData()
        {
            return new LevelBadgeData
            {
                Visible = _visibleCheck.ButtonPressed,
                FontSize = (float)_fontSizeSlider.Value,
                TextColor = _textColorBtn.Modulate,
                Text = _textEdit.Text,
                OffsetX = _offsetXCenterCheck.ButtonPressed ? 0 : (float)_offsetXSlider.Value,
                CenterX = _offsetXCenterCheck.ButtonPressed,
                OffsetY = (float)_offsetYSlider.Value,
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;
            _visibleCheck.Toggled += OnBool;
            _textColorBtn.Pressed += OnColorPressed;
            _textEdit.TextChanged += OnStr;
            _fontSizeSlider.ValueChanged += OnDbl;
            _offsetXSlider.ValueChanged += OnDbl;
            _offsetXCenterCheck.Toggled += OnCenterXToggled;
            _offsetYSlider.ValueChanged += OnDbl;
        }

        public void DisconnectSignals()
        {
            _visibleCheck.Toggled -= OnBool;
            _textColorBtn.Pressed -= OnColorPressed;
            _textEdit.TextChanged -= OnStr;
            _fontSizeSlider.ValueChanged -= OnDbl;
            _offsetXSlider.ValueChanged -= OnDbl;
            _offsetXCenterCheck.Toggled -= OnCenterXToggled;
            _offsetYSlider.ValueChanged -= OnDbl;
        }

        public void SyncFromEntity(EntityBase entity)
        {
            if (entity is not Player p) return;
            SetCheckSilent(_visibleCheck, p.LevelBadgeVisible);
            _textEdit.Text = p.LevelBadgeText ?? "";
            SetSliderSilent(_fontSizeSlider, p.LevelBadgeFontSize, _fontSizeValue, p.LevelBadgeFontSize.ToString("F0"));
            _textColorBtn.Modulate = p.LevelBadgeTextColor;
            SetSliderSilent(_offsetXSlider, p.LevelBadgeOffset.X, _offsetXValue, ((int)p.LevelBadgeOffset.X).ToString());
            SetSliderSilent(_offsetYSlider, p.LevelBadgeOffset.Y, _offsetYValue, ((int)p.LevelBadgeOffset.Y).ToString());
        }

        public void SetPropertyLocked(string propertyName, bool locked) { /* No lockable properties */ }

        public void SetCollapsed(bool collapsed) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }
        public void Dispose() { DisconnectSignals(); }

        void OnDbl(double _) => _onChanged?.Invoke();
        void OnBool(bool _) => _onChanged?.Invoke();
        void OnStr(string _) => _onChanged?.Invoke();

        void OnColorPressed()
        {
            var cur = _textColorBtn.Modulate; int next = 0;
            for (int i = 0; i < BadgeColors.Length; i++) if (cur.IsEqualApprox(BadgeColors[i])) { next = (i + 1) % BadgeColors.Length; break; }
            _textColorBtn.Modulate = BadgeColors[next]; _onChanged?.Invoke();
        }

        void OnCenterXToggled(bool centered)
        {
            _offsetXSlider.Editable = !centered;
            _offsetXSlider.Modulate = centered ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
            if (centered) { _offsetXSlider.SetBlockSignals(true); _offsetXSlider.Value = 0; _offsetXSlider.SetBlockSignals(false); _offsetXValue.Text = "0"; }
            _onChanged?.Invoke();
        }

        static void SetSliderSilent(HSlider s, double v, Label l, string t)
        { s?.SetBlockSignals(true); if (s != null) s.Value = v; s?.SetBlockSignals(false); if (l != null) l.Text = t; }
        static void SetCheckSilent(CheckButton c, bool v)
        { c?.SetBlockSignals(true); if (c != null) c.ButtonPressed = v; c?.SetBlockSignals(false); }
    }
}