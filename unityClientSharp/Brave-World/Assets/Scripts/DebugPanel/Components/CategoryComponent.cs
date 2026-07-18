using UnityEngine;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel.Components
{
    /// <summary>分类组件控件（UGUI 版，移植自 Godot CategoryComponent）。</summary>
    public class CategoryComponent : IEntityTabComponent
    {
        public string ComponentName => "category";
        public string DisplayName => "分类";
        public System.Type DataType => typeof(CategoryData);

        private UnityEngine.RectTransform _root;
        private TMPro.TMP_InputField _cat;
        private System.Action _onChanged;

        public void BuildUI(Transform parent)
        {
            _root = DebugPanelUI.Section((UnityEngine.RectTransform)parent, "分类");
            _cat = DebugPanelUI.AddInput(_root, "分类名", "", _ => _onChanged?.Invoke());
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is CategoryData d) _cat.text = d.Category ?? "";
        }

        public IComponentData SyncToData() => new CategoryData { Category = _cat.text };

        public void ConnectSignals(System.Action onChanged) => _onChanged = onChanged;
        public void DisconnectSignals() => _onChanged = null;
        public void SetPropertyLocked(string p, bool l) { }
        public void SetCollapsed(bool c) { }
        public void Dispose() => DisconnectSignals();
    }
}
