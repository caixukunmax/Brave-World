using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>等级徽章组件控件（UGUI 版，移植自 Godot LevelBadgeComponent）。</summary>
    public class LevelBadgeComponent : IEntityTabComponent
    {
        public string ComponentName => "levelbadge";
        public string DisplayName => "等级徽章";
        public System.Type DataType => typeof(LevelBadgeData);

        private RectTransform _root;
        private Toggle _visible, _centerX;
        private DebugPanelUI.SliderBinding _font, _ox, _oy;
        private DebugPanelUI.ColorBinding _color;
        private TMPro.TMP_InputField _text;
        private System.Action _onChanged;
        private bool _suppress;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((RectTransform)parent, "等级徽章");
            _visible = DebugPanelUI.AddToggle(_root, "显示", true, _ => Changed());
            _font = DebugPanelUI.AddSlider(_root, "字号", 0, 48, 12, "int", _ => Changed());
            _centerX = DebugPanelUI.AddToggle(_root, "水平居中", false, _ => Changed());
            _ox = DebugPanelUI.AddSlider(_root, "X偏移", -100f, 100f, -35f, "F1", _ => Changed());
            _oy = DebugPanelUI.AddSlider(_root, "Y偏移", -100f, 100f, -35f, "F1", _ => Changed());
            _text = DebugPanelUI.AddInput(_root, "文本模板", "Lv.{level}", _ => Changed());
            _color = DebugPanelUI.AddColorField(_root, "文字颜色", new Color(1f, 1f, 0f), _ => Changed());
        }

        private void Changed() { if (!_suppress) _onChanged?.Invoke(); }

        public void SyncFromData(IComponentData data)
        {
            if (data is LevelBadgeData d)
            {
                _suppress = true;
                _visible.isOn = d.Visible;
                _font.SetSilent(d.FontSize);
                _centerX.isOn = d.CenterX;
                _ox.SetSilent(d.OffsetX);
                _oy.SetSilent(d.OffsetY);
                _text.text = d.Text ?? "Lv.{level}";
                _color.SetSilent(d.TextColor);
                _suppress = false;
            }
        }

        public IComponentData SyncToData() => new LevelBadgeData
        {
            Visible = _visible.isOn,
            FontSize = (int)_font.Slider.value,
            CenterX = _centerX.isOn,
            OffsetX = _ox.Slider.value,
            OffsetY = _oy.Slider.value,
            Text = _text.text,
            TextColor = _color.GetValue(),
        };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
