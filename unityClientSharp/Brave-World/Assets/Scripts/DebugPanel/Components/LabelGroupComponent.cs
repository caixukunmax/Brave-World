using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>标签组组件控件（UGUI 版，移植自 Godot LabelGroupComponent，紧凑覆盖关键字段）。</summary>
    public class LabelGroupComponent : IEntityTabComponent
    {
        public string ComponentName => "labels";
        public string DisplayName => "标签";
        public System.Type DataType => typeof(LabelGroupData);

        private RectTransform _root;
        private DebugPanelUI.SliderBinding _font;
        private Toggle _bold, _italic, _shadow;
        private readonly Toggle[] _vis = new Toggle[4];
        private readonly TMPro.TMP_InputField[] _names = new TMPro.TMP_InputField[4];
        private System.Action _onChanged;
        private bool _suppress;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((RectTransform)parent, "标签");
            _font = DebugPanelUI.AddSlider(_root, "默认字号", 0, 48, 0, "int", _ => Changed());
            _bold = DebugPanelUI.AddToggle(_root, "粗体", false, _ => Changed());
            _italic = DebugPanelUI.AddToggle(_root, "斜体", false, _ => Changed());
            _shadow = DebugPanelUI.AddToggle(_root, "阴影", false, _ => Changed());
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                _vis[idx] = DebugPanelUI.AddToggle(_root, $"行{idx}显示", true, _ => Changed());
                _names[idx] = DebugPanelUI.AddInput(_root, $"行{idx}名称", "", _ => Changed());
            }
        }

        private void Changed() { if (!_suppress) _onChanged?.Invoke(); }

        public void SyncFromData(IComponentData data)
        {
            if (data is LabelGroupData d)
            {
                _suppress = true;
                _font.SetSilent(d.DefaultFontSize);
                _bold.isOn = d.Bold;
                _italic.isOn = d.Italic;
                _shadow.isOn = d.Shadow;
                for (int i = 0; i < 4; i++)
                {
                    _vis[i].isOn = d.Visible[i];
                    _names[i].text = d.Names[i] ?? "";
                }
                _suppress = false;
            }
        }

        public IComponentData SyncToData()
        {
            var d = new LabelGroupData();
            d.DefaultFontSize = (int)_font.Slider.value;
            d.Bold = _bold.isOn;
            d.Italic = _italic.isOn;
            d.Shadow = _shadow.isOn;
            for (int i = 0; i < 4; i++)
            {
                d.Visible[i] = _vis[i].isOn;
                d.Names[i] = _names[i].text;
            }
            return d;
        }

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
