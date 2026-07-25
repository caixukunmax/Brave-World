using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Rendering;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 文本背包条目 — 移植自 clinetcsharp/Scripts/TextInventoryItem.cs。
    /// 单行 "名字×数量" 文本：品质染色（<see cref="ItemIconCatalog.GetQualityColor"/>）、
    /// 悬停变黄 + 白色描边（Godot 手绘圆角框 DrawRoundedRectOutline 的 uGUI 近似，用字形 Outline 代替）、
    /// 右键/拖拽事件全部转发给 <see cref="InventoryTextContainer"/>（对齐 Godot 信号上抛）。
    /// 调试用的尺寸悬浮 tooltip（Godot DebugShowItemDimensions）依赖 Godot DebugPanel，Unity 侧未移植。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InventoryTextItem : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string ItemName { get; private set; } = "";
        public uint ItemCount { get; private set; }
        public int Quality { get; private set; }
        public uint ItemId { get; private set; }

        /// <summary>悬停文字色（对齐 Godot HoverColor = Colors.Yellow）。</summary>
        public static readonly Color HoverColor = Color.yellow;

        private TextMeshProUGUI _text;
        private Outline _hoverOutline;
        private CanvasGroup _canvasGroup;
        private Color _normalColor = Color.white;
        private InventoryTextContainer _container;

        public RectTransform Rect => (RectTransform)transform;

        public static InventoryTextItem Create(Transform parent, InventoryTextContainer container, int fontSize)
        {
            var go = new GameObject("Item", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);

            // 透明底板收指针事件（raycastTarget 三件套：底板 Image 必须开 raycastTarget）
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            FontUtil.ApplyCjkFont(tmp);

            // 悬停描边（默认关），对齐 Godot BackpackHoverBoxColor = 白
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.enabled = false;

            var item = go.AddComponent<InventoryTextItem>();
            item._text = tmp;
            item._hoverOutline = outline;
            item._container = container;
            item._canvasGroup = go.AddComponent<CanvasGroup>();
            return item;
        }

        public void Setup(string itemName, uint count, int quality, uint itemId)
        {
            ItemName = itemName;
            ItemCount = count;
            Quality = quality;
            ItemId = itemId;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            _text.text = $"{ItemName}×{ItemCount}";
            _normalColor = ItemIconCatalog.GetQualityColor(Quality);
            _text.color = _normalColor;
        }

        public Vector2 AnchoredPosition
        {
            get => Rect.anchoredPosition;
            set => Rect.anchoredPosition = value;
        }

        public void SetSize(Vector2 size) => Rect.sizeDelta = size;

        /// <summary>拖拽中半透明（对齐 Godot _GetDragData 的 Modulate 0.5 / NotificationDragEnd 恢复）。</summary>
        public void SetDragDimmed(bool dimmed) => _canvasGroup.alpha = dimmed ? 0.5f : 1f;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _text.color = HoverColor;
            if (_hoverOutline != null) _hoverOutline.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _text.color = _normalColor;
            if (_hoverOutline != null) _hoverOutline.enabled = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                _container.NotifyItemRightClicked(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _container.NotifyBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_container != null) _container.NotifyDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_container != null) _container.NotifyEndDrag(this, eventData);
        }
    }
}
