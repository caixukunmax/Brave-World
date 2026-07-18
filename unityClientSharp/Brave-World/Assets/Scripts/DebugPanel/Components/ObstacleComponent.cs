using UnityEngine;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>障碍组件控件（UGUI 版，移植自 Godot ObstacleComponent）。</summary>
    public class ObstacleComponent : IEntityTabComponent
    {
        public string ComponentName => "obstacle";
        public string DisplayName => "障碍";
        public System.Type DataType => typeof(ObstacleData);

        private UnityEngine.RectTransform _root;
        private UnityEngine.UI.Toggle _block;
        private System.Action _onChanged;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((UnityEngine.RectTransform)parent, "障碍");
            _block = DebugPanelUI.AddToggle(_root, "阻挡移动", true, _ => _onChanged?.Invoke());
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is ObstacleData d) _block.isOn = d.BlockMovement;
        }

        public IComponentData SyncToData() => new ObstacleData { BlockMovement = _block.isOn };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
