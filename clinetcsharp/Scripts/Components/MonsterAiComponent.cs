using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 怪物AI组件 — 移速、巡逻范围、仇恨范围、移动间隔、保存配置按钮
    /// </summary>
    public class MonsterAiComponent : IEntityTabComponent
    {
        public string ComponentName => "monster_ai";
        public string DisplayName => "怪物AI";
        public Type DataType => typeof(MonsterAiData);

        private Action _onChanged;

        private HSlider _moveSpeedSlider;
        private Button _moveSpeedValue;
        private HSlider _patrolRangeSlider;
        private Button _patrolRangeValue;
        private HSlider _aggroRangeSlider;
        private Button _aggroRangeValue;
        private HSlider _moveIntervalSlider;
        private Button _moveIntervalValue;
        private Button _saveConfigBtn;

        public void BuildUI(VBoxContainer parent)
        {
            (_moveSpeedSlider, _moveSpeedValue) = MakeSR(parent, "移速(ms)", 100, 3000, 800, 50);
            (_patrolRangeSlider, _patrolRangeValue) = MakeSR(parent, "巡逻范围", 0, 10, 3, 1);
            (_aggroRangeSlider, _aggroRangeValue) = MakeSR(parent, "仇恨范围", 0, 20, 5, 1);
            (_moveIntervalSlider, _moveIntervalValue) = MakeSR(parent, "移动间隔(ms)", 100, 10000, 2000, 100);

            _saveConfigBtn = new Button { Text = "保存配置到 JSON", CustomMinimumSize = new Vector2(0, 32) };
            parent.AddChild(_saveConfigBtn);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not MonsterAiData d) return;
            SetSliderSilent(_moveSpeedSlider, d.MoveSpeedMs, _moveSpeedValue, d.MoveSpeedMs.ToString());
            SetSliderSilent(_patrolRangeSlider, d.PatrolRange, _patrolRangeValue, d.PatrolRange.ToString());
            SetSliderSilent(_aggroRangeSlider, d.AggroRange, _aggroRangeValue, d.AggroRange.ToString());
            SetSliderSilent(_moveIntervalSlider, d.MoveIntervalMs, _moveIntervalValue, d.MoveIntervalMs.ToString());
        }

        public IComponentData SyncToData()
        {
            return new MonsterAiData
            {
                MoveSpeedMs = (int)_moveSpeedSlider.Value,
                PatrolRange = (float)_patrolRangeSlider.Value,
                AggroRange = (float)_aggroRangeSlider.Value,
                MoveIntervalMs = (int)_moveIntervalSlider.Value,
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;
            _moveSpeedSlider.ValueChanged += OnDbl;
            _patrolRangeSlider.ValueChanged += OnDbl;
            _aggroRangeSlider.ValueChanged += OnDbl;
            _moveIntervalSlider.ValueChanged += OnDbl;
            _saveConfigBtn.Pressed += OnSaveConfigPressed;
        }

        public void DisconnectSignals()
        {
            _moveSpeedSlider.ValueChanged -= OnDbl;
            _patrolRangeSlider.ValueChanged -= OnDbl;
            _aggroRangeSlider.ValueChanged -= OnDbl;
            _moveIntervalSlider.ValueChanged -= OnDbl;
            _saveConfigBtn.Pressed -= OnSaveConfigPressed;
        }

        public void SyncFromEntity(EntityBase entity)
        {
            // Monster AI values come from MonsterConfigManager, not EntityBase directly
            // We read from the scene tree
            var cm = entity?.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            if (cm == null) return;

            int moveSpeed = cm.GetMoveSpeedMs();
            var ai = cm.GetAiDefaults("patrol_chase");
            int patrolRange = ai.PatrolRange ?? 3;
            int aggroRange = ai.AggroRange ?? 5;
            int moveInterval = (int)(ai.MoveIntervalMs ?? 2000);

            SetSliderSilent(_moveSpeedSlider, moveSpeed, _moveSpeedValue, moveSpeed.ToString());
            SetSliderSilent(_patrolRangeSlider, patrolRange, _patrolRangeValue, patrolRange.ToString());
            SetSliderSilent(_aggroRangeSlider, aggroRange, _aggroRangeValue, aggroRange.ToString());
            SetSliderSilent(_moveIntervalSlider, moveInterval, _moveIntervalValue, moveInterval.ToString());
        }

        public void SetPropertyLocked(string propertyName, bool locked) { /* No lockable properties */ }

        public void SetCollapsed(bool collapsed) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }
        public void Dispose() { DisconnectSignals(); }

        void OnDbl(double _) => _onChanged?.Invoke();

        void OnSaveConfigPressed()
        {
            var tree = _saveConfigBtn?.GetTree();
            var cm = tree?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            if (cm == null) { GD.PushError("[MonsterAiComponent] MonsterConfigManager not found"); return; }

            cm.SetMoveSpeedMs((int)_moveSpeedSlider.Value);
            var ai = new AiDefaults
            {
                PatrolRange = (int)_patrolRangeSlider.Value,
                AggroRange = (int)_aggroRangeSlider.Value,
                MoveIntervalMs = (int)_moveIntervalSlider.Value,
                ChaseIntervalMs = (int)_moveIntervalSlider.Value / 4,
            };
            cm.SetAiDefaults("patrol_chase", ai);
            cm.SetAiDefaults("patrol", new AiDefaults
            {
                PatrolRange = (int)_patrolRangeSlider.Value,
                MoveIntervalMs = (int)_moveIntervalSlider.Value,
            });
            cm.SetAiDefaults("guard", new AiDefaults
            {
                PatrolRange = 0,
                AggroRange = (int)_aggroRangeSlider.Value,
                MoveIntervalMs = (int)_moveIntervalSlider.Value,
            });
            cm.SaveConfig();
            GD.Print("[MonsterAiComponent] Monster config saved to JSON");
        }

        static (HSlider, Button) MakeSR(Container p, string lbl, double min, double max, double def, double step)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = lbl + ":", CustomMinimumSize = new Vector2(80, 0) });
            var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = def, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            row.AddChild(s);
            Func<double, string> fmt = max <= 1 ? (v => v.ToString("F2")) : (Func<double, string>)(v => ((int)v).ToString());
            var v = new Label { Text = fmt(def), CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(v);
            p.AddChild(row);
            var valueDisplay = SliderValueInput.Attach(s, v, fmt);
            return (s, valueDisplay);
        }

        static void SetSliderSilent(HSlider s, double v, Button l, string t)
        { s?.SetBlockSignals(true); if (s != null) s.Value = v; s?.SetBlockSignals(false); if (l != null) l.Text = t; }
    }
}