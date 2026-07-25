using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityClientSharp.Map.Rendering;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 文本背包容器 — 移植自 clinetcsharp/Scripts/TextInventoryContainer.cs。
    /// 负责：按 <see cref="TextInventoryLayout"/> 流式排布条目、左键拖拽排序预览（其他条目
    /// ease-out quad 动画避让）、插入位置计算、落盘提交与取消/失败回滚、容量统计、空背包提示。
    ///
    /// 与 Godot 的实现差异（行为对齐，机制不同）：
    /// - Godot 用引擎拖拽（_GetDragData/_CanDropData/_DropData + SetDragPreview），被拖条目本体不动、
    ///   引擎托管跟随预览；uGUI 用 IBeginDrag/IDrag/IEndDrag（由条目转发），被拖条目本体跟随鼠标，
    ///   因此取消预览时需要额外把被拖条目吸附回原位。
    /// - 指针拖出容器区域即取消预览（对齐 Godot NotificationMouseExit → CancelDragPreview），
    ///   拖回容器内时按 Godot _CanDropData 的懒初始化重新建立拖拽快照。
    /// - 位置动画用容器 Update 手写 ease-out quad 插值（对齐 Godot Tween EaseOut+Quad，0.15s）。
    ///
    /// 内部坐标约定：容器 pivot 固定在左上角 (0,1)，所有布局计算在"向下为正"坐标系进行
    /// （与 Godot 2D 一致），写 RectTransform.anchoredPosition 时 y 取反。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InventoryTextContainer : MonoBehaviour
    {
        public int LineWidth = 30;
        public int LineCount = 10;
        public int FontSize = 16;
        public float LineSpacing = 8f;
        public float ItemSpacing = 8f;
        /// <summary>对齐 Godot InventoryUI.BackpackReorderAnimationDuration。</summary>
        public float ReorderAnimationDuration = 0.15f;

        /// <summary>条目右键（itemId, count），对齐 Godot ItemRightClicked 信号。</summary>
        public event Action<uint, uint> ItemRightClicked;
        /// <summary>拖拽落盘后的新顺序请求，对齐 Godot 容器内 SendReorderItems → manager。</summary>
        public event Action<List<uint>> ReorderRequested;

        private List<TextInventoryLayout.ItemEntry> _entries = new();
        private readonly List<InventoryTextItem> _items = new();
        private GameObject _emptyHint;

        // 拖拽状态（字段对齐 Godot）
        private bool _isDragging;
        private InventoryTextItem _draggedItem;
        private bool _dragArmed; // uGUI 差异：只有左键 BeginDrag 才武装拖拽（过滤右键拖拽）
        private InventoryTextItem _armedItem;
        private Vector2 _dragGrabOffset; // 指针相对条目左上角的偏移（向下为正）
        private List<TextInventoryLayout.ItemEntry> _dragStartEntries = new();
        private List<TextInventoryLayout.ItemEntry> _previewEntries = new();
        private List<Rect> _dragStartRects = new();

        // 位置动画（对齐 Godot _positionTweens）
        private class PosAnim
        {
            public InventoryTextItem Item;
            public Vector2 From; // 向下为正
            public Vector2 To;
            public float Elapsed;
        }
        private readonly List<PosAnim> _anims = new();

        public IReadOnlyList<TextInventoryLayout.ItemEntry> Entries => _entries;

        private RectTransform Rect => (RectTransform)transform;

        // ============ 数据与重建 ============

        public void SetItems(List<TextInventoryLayout.ItemEntry> entries)
        {
            _entries = entries;
            Rebuild();
        }

        /// <summary>容量统计（已用 / 总容量），逐行移植 Godot GetCapacity。</summary>
        public (int used, int total) GetCapacity()
        {
            int total = LineWidth * LineCount;
            try
            {
                int used = 0;
                var lines = TextInventoryLayout.Reflow(_entries, LineWidth, ItemSpacing / FontSize);
                foreach (var line in lines)
                {
                    for (int i = 0; i < line.Count; i++)
                    {
                        used += (int)Mathf.Ceil(TextInventoryLayout.MeasureItemWidth(line[i].Name, line[i].Count));
                        if (i < line.Count - 1)
                            used += (int)Mathf.Ceil(ItemSpacing / FontSize);
                    }
                }
                return (used, total);
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogWarning($"[InventoryTextContainer] Cannot fit items: {ex.Message}");
                return (total, total);
            }
        }

        private void Rebuild()
        {
            foreach (var item in _items)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _items.Clear();
            _anims.Clear();
            _isDragging = false;
            _draggedItem = null;
            _dragArmed = false;
            _armedItem = null;
            _dragStartEntries.Clear();
            _previewEntries.Clear();
            _dragStartRects.Clear();

            foreach (var entry in _entries)
            {
                var item = InventoryTextItem.Create(transform, this, FontSize);
                item.Setup(entry.Name, entry.Count, entry.Quality, entry.ItemId);
                _items.Add(item);
            }

            ArrangeItems();
            UpdateEmptyHint();
        }

        private void UpdateEmptyHint()
        {
            if (_items.Count > 0)
            {
                if (_emptyHint != null) _emptyHint.SetActive(false);
                return;
            }
            if (_emptyHint == null)
            {
                _emptyHint = new GameObject("EmptyHint", typeof(RectTransform));
                _emptyHint.transform.SetParent(transform, false);
                var rt = (RectTransform)_emptyHint.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var tmp = _emptyHint.AddComponent<TextMeshProUGUI>();
                tmp.text = "（空）";
                tmp.fontSize = FontSize;
                tmp.color = new Color(0.4f, 0.4f, 0.4f);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                FontUtil.ApplyCjkFont(tmp);
            }
            _emptyHint.SetActive(true);
        }

        // ============ 布局 ============

        /// <summary>按 entries 顺序计算每条目的左上角位置（向下为正），结果与 entries 等长平行。</summary>
        private List<Vector2> ComputePositions(List<TextInventoryLayout.ItemEntry> entries)
        {
            var positions = new List<Vector2>(entries.Count);
            float xUnit = FontSize;
            float yOffset = 0f;

            var lines = TextInventoryLayout.Reflow(entries, LineWidth, ItemSpacing / FontSize);
            foreach (var line in lines)
            {
                float xOffset = 0f;
                for (int i = 0; i < line.Count; i++)
                {
                    float itemWidth = TextInventoryLayout.MeasureItemWidth(line[i].Name, line[i].Count) * xUnit;
                    float spacing = i > 0 ? ItemSpacing : 0f;

                    xOffset += spacing;
                    positions.Add(new Vector2(xOffset, yOffset));
                    xOffset += itemWidth;
                }
                yOffset += FontSize + LineSpacing;
            }
            return positions;
        }

        /// <summary>立即排布（对齐 Godot ArrangeItems）。</summary>
        private void ArrangeItems()
        {
            var positions = ComputePositions(_entries);
            for (int i = 0; i < _items.Count && i < positions.Count; i++)
            {
                var item = _items[i];
                float itemWidth = TextInventoryLayout.MeasureItemWidth(item.ItemName, item.ItemCount) * FontSize;
                item.AnchoredPosition = ToAnchored(positions[i]);
                item.SetSize(new Vector2(itemWidth, FontSize));
            }
        }

        private void AnimateToPreviewLayout() => AnimateToLayout(_previewEntries);

        /// <summary>对齐 Godot AnimateToLayout：预览布局下非被拖条目动画避让。</summary>
        private void AnimateToLayout(List<TextInventoryLayout.ItemEntry> entries)
        {
            if (entries.Count == 0 || _dragStartEntries.Count == 0)
                return;

            var positions = ComputePositions(entries);
            for (int idx = 0; idx < entries.Count; idx++)
            {
                var entry = entries[idx];
                int sourceIndex = _dragStartEntries.IndexOf(entry);
                if (sourceIndex < 0 || sourceIndex >= _items.Count)
                    continue;

                var item = _items[sourceIndex];
                if (_draggedItem != null && item == _draggedItem)
                    continue;

                AnimateItemPosition(item, positions[idx]);
            }
        }

        private void AnimateItemPosition(InventoryTextItem item, Vector2 targetPosition)
        {
            if (item == null)
                return;

            _anims.RemoveAll(a => a.Item == item);
            if (ReorderAnimationDuration <= 0f)
            {
                item.AnchoredPosition = ToAnchored(targetPosition);
                return;
            }
            _anims.Add(new PosAnim
            {
                Item = item,
                From = FromAnchored(item.AnchoredPosition),
                To = targetPosition,
                Elapsed = 0f,
            });
        }

        private void CancelPositionTweens() => _anims.Clear();

        private void Update()
        {
            // ease-out quad 位置插值（对齐 Godot Tween EaseType.Out + TransitionType.Quad）
            for (int i = _anims.Count - 1; i >= 0; i--)
            {
                var a = _anims[i];
                if (a.Item == null)
                {
                    _anims.RemoveAt(i);
                    continue;
                }
                a.Elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(a.Elapsed / ReorderAnimationDuration);
                float e = 1f - (1f - t) * (1f - t);
                a.Item.AnchoredPosition = ToAnchored(Vector2.LerpUnclamped(a.From, a.To, e));
                if (t >= 1f)
                    _anims.RemoveAt(i);
            }
        }

        // ============ 坐标换算 ============

        /// <summary>向下为正坐标 → anchoredPosition（容器 pivot 固定左上角，y 取反）。</summary>
        private static Vector2 ToAnchored(Vector2 downPositive) => new Vector2(downPositive.x, -downPositive.y);

        private static Vector2 FromAnchored(Vector2 anchored) => new Vector2(anchored.x, -anchored.y);

        /// <summary>屏幕坐标 → 容器内向下为正坐标（Overlay Canvas，相机为 null）。</summary>
        private Vector2 ScreenToLocalDown(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screenPos, null, out var local);
            return new Vector2(local.x, -local.y);
        }

        private bool IsPointerInside(Vector2 screenPos)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(Rect, screenPos, null);
        }

        // ============ 条目事件（由 InventoryTextItem 转发） ============

        public void NotifyItemRightClicked(InventoryTextItem item)
        {
            ItemRightClicked?.Invoke(item.ItemId, item.ItemCount);
        }

        public void NotifyBeginDrag(InventoryTextItem item, PointerEventData eventData)
        {
            // 对齐 Godot _GetDragData：拖拽开始，条目半透明；快照懒到首次 OnDrag 建立
            _dragArmed = true;
            _armedItem = item;
            item.SetDragDimmed(true);
            _dragGrabOffset = ScreenToLocalDown(eventData.position) - FromAnchored(item.AnchoredPosition);
        }

        public void NotifyDrag(InventoryTextItem item, PointerEventData eventData)
        {
            if (!_dragArmed || item != _armedItem)
                return;

            if (!_isDragging || _draggedItem != item)
            {
                // 对齐 Godot _CanDropData 首次进入：建立拖拽快照
                _dragStartEntries = new List<TextInventoryLayout.ItemEntry>(_entries);
                _previewEntries = new List<TextInventoryLayout.ItemEntry>(_entries);
                _dragStartRects = new List<Rect>(_items.Count);
                foreach (var it in _items)
                    _dragStartRects.Add(new Rect(FromAnchored(it.AnchoredPosition), it.Rect.sizeDelta));
                _isDragging = true;
                _draggedItem = item;
            }

            // 被拖条目跟随鼠标（Godot 由引擎拖拽预览承担）
            Vector2 localDown = ScreenToLocalDown(eventData.position);
            item.AnchoredPosition = ToAnchored(localDown - _dragGrabOffset);

            // 指针拖出容器 → 取消预览（对齐 Godot NotificationMouseExit → CancelDragPreview）
            if (!IsPointerInside(eventData.position))
            {
                CancelDragPreview();
                return;
            }

            int fromIndex = _items.IndexOf(item);
            if (fromIndex < 0)
            {
                CancelDragPreview();
                return;
            }

            int insertIndex = CalculateInsertIndex(localDown);

            var preview = new List<TextInventoryLayout.ItemEntry>(_entries);
            var entry = preview[fromIndex];
            preview.RemoveAt(fromIndex);

            int toIndex = insertIndex;
            if (toIndex > fromIndex)
                toIndex--;
            preview.Insert(toIndex, entry);

            if (!EntriesEqual(_previewEntries, preview))
            {
                _previewEntries = preview;
                AnimateToPreviewLayout();
            }
        }

        public void NotifyEndDrag(InventoryTextItem item, PointerEventData eventData)
        {
            if (item == null) return; // 拖拽中背包被刷新重建（如拾取广播到达），条目已销毁
            // 对齐 Godot NotificationDragEnd：无论结果如何恢复不透明
            item.SetDragDimmed(false);
            if (!_dragArmed || item != _armedItem)
                return;
            _dragArmed = false;
            _armedItem = null;

            if (!_isDragging || _draggedItem != item)
                return; // 预览已取消（落点在容器外），对齐 Godot：_DropData 只在容器内触发

            int fromIndex = _items.IndexOf(item);
            if (fromIndex < 0)
            {
                ClearDragState();
                return;
            }

            int toIndex = CalculateInsertIndex(ScreenToLocalDown(eventData.position));
            if (toIndex == fromIndex || toIndex == fromIndex + 1)
            {
                CancelDragPreview();
                return;
            }

            // 落盘提交（对齐 Godot _DropData）
            var entry = _entries[fromIndex];
            _entries.RemoveAt(fromIndex);
            _items.RemoveAt(fromIndex);

            int insertIndex = toIndex;
            if (insertIndex > fromIndex)
                insertIndex--;
            _entries.Insert(insertIndex, entry);
            _items.Insert(insertIndex, item);

            CancelPositionTweens();
            ArrangeItems(); // 全部被拖/避让条目吸附到最终位置

            SendReorderItems();
            ClearDragState();
        }

        // ============ 拖拽内部 ============

        private void CancelDragPreview()
        {
            if (!_isDragging)
                return;

            _entries = new List<TextInventoryLayout.ItemEntry>(_dragStartEntries);
            _previewEntries = new List<TextInventoryLayout.ItemEntry>(_dragStartEntries);

            // 注意：Godot 在此按 ItemId 找回条目重建 _items，但预览期间 _items 顺序从未改变
            // （重排只发生在落盘提交），该重建实为防御性无操作；且按 ItemId 查找在同 id 多条
            // （拆堆叠）时会错配，故 Unity 侧跳过重建，_items 保持拖拽起始顺序。
            AnimateToPreviewLayout();

            // uGUI 差异：被拖条目本体跟随鼠标，取消预览时吸附回原位（Godot 本体从未移动）
            if (_draggedItem != null)
            {
                int idx = _items.IndexOf(_draggedItem);
                if (idx >= 0)
                {
                    var positions = ComputePositions(_entries);
                    if (idx < positions.Count)
                        _draggedItem.AnchoredPosition = ToAnchored(positions[idx]);
                }
            }

            _isDragging = false;
            _draggedItem = null;
            _dragStartEntries.Clear();
            _previewEntries.Clear();
            _dragStartRects.Clear();
        }

        private void ClearDragState()
        {
            _isDragging = false;
            _draggedItem = null;
            _dragStartEntries.Clear();
            _previewEntries.Clear();
            _dragStartRects.Clear();
        }

        private void SendReorderItems()
        {
            var orderedIds = new List<uint>(_entries.Count);
            foreach (var entry in _entries)
                orderedIds.Add(entry.ItemId);
            ReorderRequested?.Invoke(orderedIds);
        }

        /// <summary>插入位置计算，逐行移植 Godot CalculateInsertIndex（向下为正坐标）。</summary>
        private int CalculateInsertIndex(Vector2 position)
        {
            for (int i = 0; i < _dragStartRects.Count; i++)
            {
                var rect = _dragStartRects[i];
                var center = rect.position + rect.size / 2;
                if (position.y < center.y || (position.y < center.y + rect.size.y / 2 && position.x < center.x))
                    return i;
            }
            return _items.Count;
        }

        private static bool EntriesEqual(List<TextInventoryLayout.ItemEntry> a, List<TextInventoryLayout.ItemEntry> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!ReferenceEquals(a[i], b[i]))
                    return false;
            }
            return true;
        }
    }
}
