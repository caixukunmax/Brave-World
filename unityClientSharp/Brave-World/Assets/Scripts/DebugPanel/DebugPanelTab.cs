using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// Tab 基类：每个 Tab 在 Scroll 内容区构建一个纵向布局容器，并实现 BuildContent 填充内容。
    /// shell 通过 Root.gameObject.SetActive 切换显隐。
    /// </summary>
    public abstract class DebugPanelTab
    {
        public RectTransform Root { get; private set; }
        public abstract string TabName { get; }

        public void Build(RectTransform parent)
        {
            var go = new GameObject("Tab:" + TabName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Root = (RectTransform)go.transform;
            BuildContent(Root);
        }

        protected abstract void BuildContent(RectTransform root);

        public virtual void OnShow() { }
        public virtual void OnHide() { }
    }
}
