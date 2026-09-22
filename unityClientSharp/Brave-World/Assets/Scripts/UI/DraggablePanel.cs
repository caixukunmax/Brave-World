using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 可拖拽/可拉伸面板通用件：
    /// - <see cref="DragHandle"/> 挂标题栏：拖动移动整个面板（限制不出屏）。
    /// - <see cref="ResizeHandle"/> 挂右下角手柄：拖动拉伸面板尺寸（带最小尺寸）。
    /// </summary>
    public static class DraggablePanel
    {
        /// <summary>给面板加标题栏（拖动移动）+ 右下角拉伸柄。返回标题文本组件。</summary>
        /// <param name="onClose">可选：传了就在标题栏右侧加 X 关闭按钮，点击触发该回调（面板显隐由回调决定）。</param>
        public static TMPro.TextMeshProUGUI MakeDraggable(RectTransform panel, string title, Vector2 minSize, System.Action onClose = null)
        {
            // 拖拽依赖 EventSystem，防御性确保存在（正常流程 MapBootstrap 已建）
            UiEventSystemUtil.EnsureExists();
            // 标题栏
            var titleBar = new GameObject("TitleBar", typeof(RectTransform));
            titleBar.transform.SetParent(panel, false);
            var titleRt = (RectTransform)titleBar.transform;
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.anchoredPosition = Vector2.zero;
            titleRt.sizeDelta = new Vector2(0, 24);
            var titleImg = titleBar.AddComponent<Image>();
            titleImg.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);
            var drag = titleBar.AddComponent<DragHandle>();
            drag.Target = panel;

            var textGo = new GameObject("Title", typeof(RectTransform));
            textGo.transform.SetParent(titleBar.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 0);
            textRt.offsetMax = new Vector2(-8, 0);
            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 13;
            tmp.color = new Color(1f, 0.9f, 0.6f);
            tmp.alignment = TMPro.TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            Map.Rendering.FontUtil.ApplyCjkFont(tmp);

            // 可选：标题栏右侧 X 关闭按钮（20×20）
            if (onClose != null)
            {
                var closeGo = new GameObject("CloseBtn", typeof(RectTransform));
                closeGo.transform.SetParent(titleBar.transform, false);
                var closeRt = (RectTransform)closeGo.transform;
                closeRt.anchorMin = new Vector2(1, 0.5f);
                closeRt.anchorMax = new Vector2(1, 0.5f);
                closeRt.pivot = new Vector2(1, 0.5f);
                closeRt.anchoredPosition = new Vector2(-2, 0);
                closeRt.sizeDelta = new Vector2(20, 20);
                var closeImg = closeGo.AddComponent<Image>();
                closeImg.color = new Color(0.4f, 0.2f, 0.2f, 0.9f);
                var closeBtn = closeGo.AddComponent<Button>();
                closeBtn.onClick.AddListener(() => onClose());
                var closeTextGo = new GameObject("Text", typeof(RectTransform));
                closeTextGo.transform.SetParent(closeGo.transform, false);
                var closeTextRt = (RectTransform)closeTextGo.transform;
                closeTextRt.anchorMin = Vector2.zero;
                closeTextRt.anchorMax = Vector2.one;
                closeTextRt.offsetMin = Vector2.zero;
                closeTextRt.offsetMax = Vector2.zero;
                var closeTmp = closeTextGo.AddComponent<TMPro.TextMeshProUGUI>();
                closeTmp.text = "×";
                closeTmp.fontSize = 14;
                closeTmp.color = Color.white;
                closeTmp.alignment = TMPro.TextAlignmentOptions.Center;
                closeTmp.raycastTarget = false;
                // 标题文本让出按钮位
                textRt.offsetMax = new Vector2(-26, 0);
            }

            // 右下角拉伸柄
            var grip = new GameObject("ResizeGrip", typeof(RectTransform));
            grip.transform.SetParent(panel, false);
            var gripRt = (RectTransform)grip.transform;
            gripRt.anchorMin = new Vector2(1, 0);
            gripRt.anchorMax = new Vector2(1, 0);
            gripRt.pivot = new Vector2(1, 0);
            gripRt.anchoredPosition = Vector2.zero;
            gripRt.sizeDelta = new Vector2(16, 16);
            var gripImg = grip.AddComponent<Image>();
            gripImg.color = new Color(0.5f, 0.5f, 0.55f, 0.9f);
            var resize = grip.AddComponent<ResizeHandle>();
            resize.Target = panel;
            resize.MinSize = minSize;

            return tmp;
        }

        public class DragHandle : MonoBehaviour, IDragHandler
        {
            public RectTransform Target;

            public void OnDrag(PointerEventData eventData)
            {
                if (Target == null) return;
                float scale = GetCanvasScale();
                Target.anchoredPosition += eventData.delta / scale;
                ClampToScreen(Target);
            }
        }

        public class ResizeHandle : MonoBehaviour, IDragHandler
        {
            public RectTransform Target;
            public Vector2 MinSize = new Vector2(200, 100);

            public void OnDrag(PointerEventData eventData)
            {
                if (Target == null) return;
                float scale = GetCanvasScale();
                var size = Target.sizeDelta;
                size.x = Mathf.Max(MinSize.x, size.x + eventData.delta.x / scale);
                size.y = Mathf.Max(MinSize.y, size.y - eventData.delta.y / scale);
                Target.sizeDelta = size;
            }
        }

        private static float GetCanvasScale()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            return canvas != null && canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
        }

        /// <summary>保证面板至少有一部分留在屏幕内（pivot 任意时按左下角近似）。</summary>
        private static void ClampToScreen(RectTransform panel)
        {
            var pos = panel.anchoredPosition;
            var size = panel.sizeDelta;
            float minX = -size.x * panel.pivot.x + 40 - size.x;
            float maxX = Screen.width - 40 + size.x * (1f - panel.pivot.x);
            float minY = -size.y * panel.pivot.y + 40 - size.y;
            float maxY = Screen.height - 40 + size.y * (1f - panel.pivot.y);
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            panel.anchoredPosition = pos;
        }
    }
}
