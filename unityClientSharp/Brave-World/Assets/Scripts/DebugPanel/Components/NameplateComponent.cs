using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>铭牌背景组件控件（UGUI 版，移植自 Godot NameplateComponent）。</summary>
    public class NameplateComponent : IEntityTabComponent
    {
        public string ComponentName => "nameplate";
        public string DisplayName => "铭牌背景";
        public System.Type DataType => typeof(NameplateData);

        private RectTransform _root;
        private DebugPanelUI.SliderBinding _yOffset, _spacing, _barH, _cbH, _cbWS;
        private DebugPanelUI.ColorBinding _bar, _cb;
        private Toggle _visible;
        private System.Action _onChanged;
        private bool _suppress;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((RectTransform)parent, "铭牌背景");
            _visible = DebugPanelUI.AddToggle(_root, "显示", false, _ => Changed());
            _yOffset = DebugPanelUI.AddSlider(_root, "Y偏移", -120f, 0f, -80f, "F1", _ => Changed());
            _spacing = DebugPanelUI.AddSlider(_root, "间距", 0f, 16f, 4f, "F1", _ => Changed());
            _barH = DebugPanelUI.AddSlider(_root, "顶/底栏高", 1f, 16f, 6f, "F1", _ => Changed());
            _cbH = DebugPanelUI.AddSlider(_root, "中块高", 8f, 48f, 24f, "F1", _ => Changed());
            _cbWS = DebugPanelUI.AddSlider(_root, "中块宽比例", 0.2f, 1f, 0.6f, "F2", _ => Changed());
            _bar = DebugPanelUI.AddColorField(_root, "顶/底栏颜色", new Color(0.1f, 0.1f, 0.1f, 0.7f), _ => Changed());
            _cb = DebugPanelUI.AddColorField(_root, "中块颜色", new Color(0.1f, 0.1f, 0.1f, 0.85f), _ => Changed());
        }

        private void Changed() { if (!_suppress) _onChanged?.Invoke(); }

        public void SyncFromData(IComponentData data)
        {
            if (data is NameplateData d)
            {
                _suppress = true;
                _visible.isOn = d.Visible;
                _yOffset.SetSilent(d.YOffset);
                _spacing.SetSilent(d.Spacing);
                _barH.SetSilent(d.BarHeight);
                _cbH.SetSilent(d.CenterBoxHeight);
                _cbWS.SetSilent(d.CenterBoxWidthScale);
                _bar.SetSilent(d.BarColor);
                _cb.SetSilent(d.CenterBoxColor);
                _suppress = false;
            }
        }

        public IComponentData SyncToData() => new NameplateData
        {
            Visible = _visible.isOn,
            YOffset = _yOffset.Slider.value,
            Spacing = _spacing.Slider.value,
            BarHeight = _barH.Slider.value,
            CenterBoxHeight = _cbH.Slider.value,
            CenterBoxWidthScale = _cbWS.Slider.value,
            BarColor = _bar.GetValue(),
            CenterBoxColor = _cb.GetValue(),
        };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
