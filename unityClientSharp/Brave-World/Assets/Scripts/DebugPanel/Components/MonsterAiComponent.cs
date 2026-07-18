using UnityEngine;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>怪物 AI 组件控件（UGUI 版，移植自 Godot MonsterAiComponent）。</summary>
    public class MonsterAiComponent : IEntityTabComponent
    {
        public string ComponentName => "monster_ai";
        public string DisplayName => "怪物AI";
        public System.Type DataType => typeof(MonsterAiData);

        private RectTransform _root;
        private DebugPanelUI.SliderBinding _moveSpeed, _patrol, _aggro, _moveInterval;
        private System.Action _onChanged;
        private bool _suppress;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((RectTransform)parent, "怪物AI");
            _moveSpeed = DebugPanelUI.AddSlider(_root, "移动速度(ms)", 100f, 3000f, 800f, "int", _ => Changed());
            _patrol = DebugPanelUI.AddSlider(_root, "巡逻范围", 0f, 20f, 3f, "F1", _ => Changed());
            _aggro = DebugPanelUI.AddSlider(_root, "仇恨范围", 0f, 30f, 5f, "F1", _ => Changed());
            _moveInterval = DebugPanelUI.AddSlider(_root, "移动间隔(ms)", 200f, 10000f, 2000f, "int", _ => Changed());
        }

        private void Changed() { if (!_suppress) _onChanged?.Invoke(); }

        public void SyncFromData(IComponentData data)
        {
            if (data is MonsterAiData d)
            {
                _suppress = true;
                _moveSpeed.SetSilent(d.MoveSpeedMs);
                _patrol.SetSilent(d.PatrolRange);
                _aggro.SetSilent(d.AggroRange);
                _moveInterval.SetSilent(d.MoveIntervalMs);
                _suppress = false;
            }
        }

        public IComponentData SyncToData() => new MonsterAiData
        {
            MoveSpeedMs = Mathf.RoundToInt(_moveSpeed.Slider.value),
            PatrolRange = _patrol.Slider.value,
            AggroRange = _aggro.Slider.value,
            MoveIntervalMs = Mathf.RoundToInt(_moveInterval.Slider.value),
        };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
