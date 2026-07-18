using UnityEngine;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>NPC 交互面板偏移组件控件（UGUI 版，移植自 Godot NpcInteractComponent）。</summary>
    public class NpcInteractComponent : IEntityTabComponent
    {
        public string ComponentName => "npc_interact";
        public string DisplayName => "交互面板";
        public System.Type DataType => typeof(NpcInteractData);

        private RectTransform _root;
        private DebugPanelUI.SliderBinding _ax, _ay, _bx, _by;
        private System.Action _onChanged;
        private bool _suppress;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((RectTransform)parent, "交互面板");
            _ax = DebugPanelUI.AddSlider(_root, "按钮A X", -200f, 200f, 60f, "F1", _ => Changed());
            _ay = DebugPanelUI.AddSlider(_root, "按钮A Y", -200f, 200f, -20f, "F1", _ => Changed());
            _bx = DebugPanelUI.AddSlider(_root, "按钮B X", -200f, 200f, -60f, "F1", _ => Changed());
            _by = DebugPanelUI.AddSlider(_root, "按钮B Y", -200f, 200f, -20f, "F1", _ => Changed());
        }

        private void Changed() { if (!_suppress) _onChanged?.Invoke(); }

        public void SyncFromData(IComponentData data)
        {
            if (data is NpcInteractData d)
            {
                _suppress = true;
                _ax.SetSilent(d.OffsetAX);
                _ay.SetSilent(d.OffsetAY);
                _bx.SetSilent(d.OffsetBX);
                _by.SetSilent(d.OffsetBY);
                _suppress = false;
            }
        }

        public IComponentData SyncToData() => new NpcInteractData
        {
            OffsetAX = _ax.Slider.value,
            OffsetAY = _ay.Slider.value,
            OffsetBX = _bx.Slider.value,
            OffsetBY = _by.Slider.value,
        };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
