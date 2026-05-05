using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 动作栏组件 — 强制显示、文字Y偏移、进度条高度
    /// </summary>
    public class ActionBarComponent : IEntityTabComponent
    {
        public string ComponentName => "actionbar";
        public string DisplayName => "动作栏";
        public Type DataType => typeof(ActionBarData);

        private Action _onChanged;

        private CheckButton _forceShowCheck;
        private HSlider _textYOffsetSlider;
        private Label _textYOffsetValue;
        private HSlider _progressHeightSlider;
        private Label _progressHeightValue;

        public void BuildUI(VBoxContainer parent)
        {
            // Title row
            var titleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            titleRow.AddChild(new Label { Text = "动作栏", CustomMinimumSize = new Vector2(45, 0) });
            _forceShowCheck = new CheckButton { ButtonPressed = false };
            titleRow.AddChild(_forceShowCheck);
            titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            parent.AddChild(titleRow);

            // Text Y offset
            var tyRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tyRow.AddChild(new Label { Text = "文字Y偏移:", CustomMinimumSize = new Vector2(70, 0) });
            _textYOffsetSlider = new HSlider { MinValue = -30, MaxValue = 30, Step = 1, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            tyRow.AddChild(_textYOffsetSlider);
            _textYOffsetValue = new Label { Text = "0", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            tyRow.AddChild(_textYOffsetValue);
            parent.AddChild(tyRow);
            SliderValueInput.Attach(_textYOffsetSlider, _textYOffsetValue, v => ((int)v).ToString());

            // Progress height
            var phRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            phRow.AddChild(new Label { Text = "进度条高度:", CustomMinimumSize = new Vector2(70, 0) });
            _progressHeightSlider = new HSlider { MinValue = 1, MaxValue = 20, Step = 1, Value = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            phRow.AddChild(_progressHeightSlider);
            _progressHeightValue = new Label { Text = "4", CustomMinimumSize = new Vector2(30, 0), HorizontalAlignment = HorizontalAlignment.Right };
            phRow.AddChild(_progressHeightValue);
            parent.AddChild(phRow);
            SliderValueInput.Attach(_progressHeightSlider, _progressHeightValue, v => ((int)v).ToString());
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not ActionBarData d) return;
            SetCheckSilent(_forceShowCheck, d.ForceShow);
            SetSliderSilent(_textYOffsetSlider, d.TextYOffset, _textYOffsetValue, ((int)d.TextYOffset).ToString());
            SetSliderSilent(_progressHeightSlider, d.ProgressHeight, _progressHeightValue, ((int)d.ProgressHeight).ToString());
        }

        public IComponentData SyncToData()
        {
            return new ActionBarData
            {
                ForceShow = _forceShowCheck.ButtonPressed,
                TextYOffset = (float)_textYOffsetSlider.Value,
                ProgressHeight = (float)_progressHeightSlider.Value,
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;
            _forceShowCheck.Toggled += OnBool;
            _textYOffsetSlider.ValueChanged += OnDbl;
            _progressHeightSlider.ValueChanged += OnDbl;
        }

        public void DisconnectSignals()
        {
            _forceShowCheck.Toggled -= OnBool;
            _textYOffsetSlider.ValueChanged -= OnDbl;
            _progressHeightSlider.ValueChanged -= OnDbl;
        }

        public void SyncFromEntity(EntityBase entity)
        {
            if (entity is not Player p) return;
            SetCheckSilent(_forceShowCheck, p.ActionBarForceShow);
            SetSliderSilent(_textYOffsetSlider, p.ActionBarTextYOffset, _textYOffsetValue, ((int)p.ActionBarTextYOffset).ToString());
            SetSliderSilent(_progressHeightSlider, p.ActionBarProgressHeight, _progressHeightValue, ((int)p.ActionBarProgressHeight).ToString());
        }

        public void SetPropertyLocked(string propertyName, bool locked) { /* No lockable properties */ }

        public void SetCollapsed(bool collapsed) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }
        public void Dispose() { DisconnectSignals(); }

        void OnDbl(double _) => _onChanged?.Invoke();
        void OnBool(bool _) => _onChanged?.Invoke();

        static void SetSliderSilent(HSlider s, double v, Label l, string t)
        { s?.SetBlockSignals(true); if (s != null) s.Value = v; s?.SetBlockSignals(false); if (l != null) l.Text = t; }
        static void SetCheckSilent(CheckButton c, bool v)
        { c?.SetBlockSignals(true); if (c != null) c.ButtonPressed = v; c?.SetBlockSignals(false); }
    }
}