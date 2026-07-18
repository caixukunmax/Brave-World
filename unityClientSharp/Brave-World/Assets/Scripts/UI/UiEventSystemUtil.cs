using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// UI 交互前提：场景里有且只有一个 EventSystem（+StandaloneInputModule）。
    /// 缺了它，Button 点击、IDragHandler 拖拽全部静默失效，Canvas 上的 GraphicRaycaster 也形同虚设；
    /// 相机/地图拖拽靠 <see cref="EventSystem.IsPointerOverGameObject()"/> 判断指针是否在 UI 上同样依赖它。
    /// </summary>
    public static class UiEventSystemUtil
    {
        /// <summary>若场景中没有 EventSystem 则创建一个；已存在则不动作。</summary>
        public static void EnsureExists()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
