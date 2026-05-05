using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// NPC交互面板偏移组件 — A位置和 B位置的 X/Y 偏移
    /// </summary>
    public class NpcInteractComponent : IEntityTabComponent
    {
        public string ComponentName => "npc_interact";
        public string DisplayName => "交互面板";
        public Type DataType => typeof(NpcInteractData);

        private Action _onChanged;

        private HSlider _offsetAXSlider;
        private Label _offsetAXValue;
        private HSlider _offsetAYSlider;
        private Label _offsetAYValue;
        private HSlider _offsetBXSlider;
        private Label _offsetBXValue;
        private HSlider _offsetBYSlider;
        private Label _offsetBYValue;

        public void BuildUI(VBoxContainer parent)
        {
            parent.AddChild(new Label { Text = "A位置(玩家在左,面板在右):" });
            (_offsetAXSlider, _offsetAXValue) = MakeSR(parent, "A-X", -200, 200, 60);
            (_offsetAYSlider, _offsetAYValue) = MakeSR(parent, "A-Y", -200, 200, -20);

            parent.AddChild(new Label { Text = "B位置(玩家在右,面板在左):" });
            (_offsetBXSlider, _offsetBXValue) = MakeSR(parent, "B-X", -200, 200, -60);
            (_offsetBYSlider, _offsetBYValue) = MakeSR(parent, "B-Y", -200, 200, -20);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not NpcInteractData d) return;
            SetSliderSilent(_offsetAXSlider, d.OffsetAX, _offsetAXValue, ((int)d.OffsetAX).ToString());
            SetSliderSilent(_offsetAYSlider, d.OffsetAY, _offsetAYValue, ((int)d.OffsetAY).ToString());
            SetSliderSilent(_offsetBXSlider, d.OffsetBX, _offsetBXValue, ((int)d.OffsetBX).ToString());
            SetSliderSilent(_offsetBYSlider, d.OffsetBY, _offsetBYValue, ((int)d.OffsetBY).ToString());
        }

        public IComponentData SyncToData()
        {
            return new NpcInteractData
            {
                OffsetAX = (float)_offsetAXSlider.Value,
                OffsetAY = (float)_offsetAYSlider.Value,
                OffsetBX = (float)_offsetBXSlider.Value,
                OffsetBY = (float)_offsetBYSlider.Value,
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;
            _offsetAXSlider.ValueChanged += OnDbl;
            _offsetAYSlider.ValueChanged += OnDbl;
            _offsetBXSlider.ValueChanged += OnDbl;
            _offsetBYSlider.ValueChanged += OnDbl;
        }

        public void DisconnectSignals()
        {
            _offsetAXSlider.ValueChanged -= OnDbl;
            _offsetAYSlider.ValueChanged -= OnDbl;
            _offsetBXSlider.ValueChanged -= OnDbl;
            _offsetBYSlider.ValueChanged -= OnDbl;
        }

        public void SyncFromEntity(EntityBase entity)
        {
            // NPC interact offsets come from NpcManager, not EntityBase directly
            if (entity == null) return;
            // EntityStyleConfig has these values; read from NpcManager if available
            var cfg = NpcManager.GetStyleConfig(entity.ProfileId > 0 ? entity.ProfileId : 1);
            if (cfg == null) return;

            SetSliderSilent(_offsetAXSlider, cfg.InteractMenuOffsetAX, _offsetAXValue, ((int)cfg.InteractMenuOffsetAX).ToString());
            SetSliderSilent(_offsetAYSlider, cfg.InteractMenuOffsetAY, _offsetAYValue, ((int)cfg.InteractMenuOffsetAY).ToString());
            SetSliderSilent(_offsetBXSlider, cfg.InteractMenuOffsetBX, _offsetBXValue, ((int)cfg.InteractMenuOffsetBX).ToString());
            SetSliderSilent(_offsetBYSlider, cfg.InteractMenuOffsetBY, _offsetBYValue, ((int)cfg.InteractMenuOffsetBY).ToString());
        }

        public void SetPropertyLocked(string propertyName, bool locked) { /* No lockable properties */ }

        public void SetCollapsed(bool collapsed) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }
        public void Dispose() { DisconnectSignals(); }

        void OnDbl(double _) => _onChanged?.Invoke();

        static (HSlider, Label) MakeSR(Container p, string lbl, double min, double max, double def)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = lbl + ":", CustomMinimumSize = new Vector2(50, 0) });
            var s = new HSlider { MinValue = min, MaxValue = max, Step = 1, Value = def, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 20), Scrollable = false, FocusMode = Control.FocusModeEnum.Click };
            row.AddChild(s);
            var v = new Label { Text = ((int)def).ToString(), CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(v);
            Func<double, string> fmt = v2 => ((int)v2).ToString();
            s.ValueChanged += (val) => v.Text = fmt(val);
            p.AddChild(row);
            SliderValueInput.Attach(s, v, fmt);
            return (s, v);
        }

        static void SetSliderSilent(HSlider s, double v, Label l, string t)
        { s?.SetBlockSignals(true); if (s != null) s.Value = v; s?.SetBlockSignals(false); if (l != null) l.Text = t; }
    }
}