using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>血条/MP条组件控件（UGUI 版，移植自 Godot BarGroupComponent）。</summary>
    public class BarGroupComponent : IEntityTabComponent
    {
        private readonly string _barName;
        private readonly string _title;

        public BarGroupComponent(string barName, string title) { _barName = barName; _title = title; }

        public string ComponentName => _barName;
        public string DisplayName => _title;
        public System.Type DataType => typeof(BarData);

        private RectTransform _root;
        private Toggle _visible, _centerX;
        private DebugPanelUI.SliderBinding _len, _hgt, _fill, _ox, _oy;
        private DebugPanelUI.ColorBinding _color;
        private System.Action _onChanged;
        private bool _suppress;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((RectTransform)parent, _title);
            _visible = DebugPanelUI.AddToggle(_root, "显示", true, _ => Changed());
            _len = DebugPanelUI.AddSlider(_root, "长度比例", 0.2f, 1.5f, 1f, "F2", _ => Changed());
            _hgt = DebugPanelUI.AddSlider(_root, "高度比例", 0.01f, 0.2f, 0.05f, "F3", _ => Changed());
            _fill = DebugPanelUI.AddSlider(_root, "填充比例", 0f, 1f, 1f, "F2", _ => Changed());
            _centerX = DebugPanelUI.AddToggle(_root, "水平居中", true, _ => Changed());
            _ox = DebugPanelUI.AddSlider(_root, "X偏移", -100f, 100f, 0f, "F1", _ => Changed());
            _oy = DebugPanelUI.AddSlider(_root, "Y偏移", -120f, 40f, -70f, "F1", _ => Changed());
            _color = DebugPanelUI.AddColorField(_root, "颜色", Color.green, _ => Changed());
        }

        private void Changed() { if (!_suppress) _onChanged?.Invoke(); }

        public void SyncFromData(IComponentData data)
        {
            if (data is BarData d)
            {
                _suppress = true;
                _visible.isOn = d.Visible;
                _len.SetSilent(d.LengthScale);
                _hgt.SetSilent(d.HeightScale);
                _fill.SetSilent(d.FillPercent);
                _centerX.isOn = d.CenterX;
                _ox.SetSilent(d.OffsetX);
                _oy.SetSilent(d.OffsetY);
                _color.SetSilent(d.Color);
                _suppress = false;
            }
        }

        public IComponentData SyncToData() => new BarData
        {
            Visible = _visible.isOn,
            LengthScale = _len.Slider.value,
            HeightScale = _hgt.Slider.value,
            FillPercent = _fill.Slider.value,
            CenterX = _centerX.isOn,
            OffsetX = _ox.Slider.value,
            OffsetY = _oy.Slider.value,
            Color = _color.GetValue(),
        };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }

    /// <summary>施法条组件控件（复用 BarGroupComponent，指向 castbar）。</summary>
    public class CastBarComponent : BarGroupComponent
    {
        public CastBarComponent() : base("castbar", "施法条") { }
    }
}
