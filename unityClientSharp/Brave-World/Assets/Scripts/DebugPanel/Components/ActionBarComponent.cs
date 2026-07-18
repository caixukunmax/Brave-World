using UnityEngine;
using UnityEngine.UI;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>动作栏组件控件（UGUI 版，移植自 Godot ActionBarComponent）。</summary>
    public class ActionBarComponent : IEntityTabComponent
    {
        public string ComponentName => "actionbar";
        public string DisplayName => "动作栏";
        public System.Type DataType => typeof(ActionBarData);

        private RectTransform _root;
        private Toggle _force;
        private DebugPanelUI.SliderBinding _ty, _ph;
        private System.Action _onChanged;
        private bool _suppress;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((RectTransform)parent, "动作栏");
            _force = DebugPanelUI.AddToggle(_root, "强制显示", false, _ => Changed());
            _ty = DebugPanelUI.AddSlider(_root, "文本Y偏移", -40f, 40f, 0f, "F1", _ => Changed());
            _ph = DebugPanelUI.AddSlider(_root, "进度条高度", 1f, 12f, 4f, "F1", _ => Changed());
        }

        private void Changed() { if (!_suppress) _onChanged?.Invoke(); }

        public void SyncFromData(IComponentData data)
        {
            if (data is ActionBarData d)
            {
                _suppress = true;
                _force.isOn = d.ForceShow;
                _ty.SetSilent(d.TextYOffset);
                _ph.SetSilent(d.ProgressHeight);
                _suppress = false;
            }
        }

        public IComponentData SyncToData() => new ActionBarData
        {
            ForceShow = _force.isOn,
            TextYOffset = _ty.Slider.value,
            ProgressHeight = _ph.Slider.value,
        };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
