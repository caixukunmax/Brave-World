using UnityEngine;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>建筑类型组件控件（UGUI 版，移植自 Godot BuildingTypeComponent）。</summary>
    public class BuildingTypeComponent : IEntityTabComponent
    {
        public string ComponentName => "building_type";
        public string DisplayName => "建筑类型";
        public System.Type DataType => typeof(BuildingTypeData);

        private UnityEngine.RectTransform _root;
        private TMPro.TMP_Dropdown _dd;
        private int[] _types;
        private System.Action _onChanged;
        private bool _suppress;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((UnityEngine.RectTransform)parent, "建筑类型");
            _types = BuildingType.GetAllTypes();
            var names = new string[_types.Length];
            for (int i = 0; i < _types.Length; i++) names[i] = BuildingType.GetDisplayName(_types[i]);
            _dd = DebugPanelUI.AddDropdown(_root, "类型", names, 0, _ => Changed());
        }

        private void Changed() { if (!_suppress) _onChanged?.Invoke(); }

        public void SyncFromData(IComponentData data)
        {
            if (data is BuildingTypeData d)
            {
                _suppress = true;
                int idx = System.Array.IndexOf(_types, d.Type);
                if (idx < 0) idx = 0;
                _dd.value = idx;
                _suppress = false;
            }
        }

        public IComponentData SyncToData() => new BuildingTypeData
        {
            Type = _types[_dd.value],
        };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
