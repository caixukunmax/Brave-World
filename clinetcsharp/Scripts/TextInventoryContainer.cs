using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    [GlobalClass]
    public partial class TextInventoryContainer : Control
    {
        [Export] public int LineWidth = 30;
        [Export] public int LineCount = 10;
        [Export] public int FontSize = 16;
        [Export] public float LineSpacing = 8f;
        [Export] public float ItemSpacing = 8f;
        [Export] public bool DrawDebugBorder { get; set; }

        private List<TextInventoryLayout.ItemEntry> _entries = new();
        private List<TextInventoryItem> _items = new();
        private int _dragTargetIndex = -1;

        private bool _isDragging;
        private TextInventoryItem? _draggedItem;
        private List<TextInventoryLayout.ItemEntry> _dragStartEntries = new();
        private List<TextInventoryLayout.ItemEntry> _previewEntries = new();
        private List<Rect2> _dragStartRects = new();
        private readonly Dictionary<TextInventoryItem, Tween> _positionTweens = new();

        public void SetItems(List<TextInventoryLayout.ItemEntry> entries)
        {
            _entries = entries;
            Rebuild();
        }

        public List<TextInventoryLayout.ItemEntry> GetItems()
        {
            return _entries;
        }

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
            catch (System.InvalidOperationException ex)
            {
                GD.PushWarning($"[TextInventoryContainer] Cannot fit items: {ex.Message}");
                return (total, total);
            }
        }

        private void Rebuild()
        {
            foreach (var child in _items)
            {
                child.RightClicked -= OnItemRightClicked;
                child.QueueFree();
            }
            _items.Clear();
            foreach (var tween in _positionTweens.Values)
            {
                tween?.Kill();
            }
            _positionTweens.Clear();
            _isDragging = false;
            _draggedItem = null;
            _dragStartEntries.Clear();
            _previewEntries.Clear();
            _dragStartRects.Clear();
            _dragTargetIndex = -1;

            foreach (var entry in _entries)
            {
                var item = new TextInventoryItem();
                item.Setup(entry.Name, entry.Count, entry.Quality, entry.ItemId);
                item.RightClicked += OnItemRightClicked;
                AddChild(item);
                _items.Add(item);
            }

            ArrangeItems();
        }

        private void ArrangeItems()
        {
            float xUnit = FontSize;
            float yOffset = 0f;
            int itemIndex = 0;

            var lines = TextInventoryLayout.Reflow(_entries, LineWidth, ItemSpacing / FontSize);
            foreach (var line in lines)
            {
                float xOffset = 0f;
                for (int i = 0; i < line.Count; i++)
                {
                    var item = _items[itemIndex];
                    float itemWidth = TextInventoryLayout.MeasureItemWidth(item.ItemName, item.ItemCount) * xUnit;
                    float spacing = i > 0 ? ItemSpacing : 0f;

                    xOffset += spacing;
                    item.Position = new Vector2(xOffset, yOffset);
                    item.Size = new Vector2(itemWidth, FontSize);
                    xOffset += itemWidth;
                    itemIndex++;
                }
                yOffset += FontSize + LineSpacing;
            }
        }

        private void AnimateToPreviewLayout()
        {
            AnimateToLayout(_previewEntries);
        }

        private void AnimateToLayout(List<TextInventoryLayout.ItemEntry> entries)
        {
            if (entries.Count == 0 || _dragStartEntries.Count == 0)
                return;

            float xUnit = FontSize;
            float yOffset = 0f;

            var lines = TextInventoryLayout.Reflow(entries, LineWidth, ItemSpacing / FontSize);
            foreach (var line in lines)
            {
                float xOffset = 0f;
                for (int i = 0; i < line.Count; i++)
                {
                    var entry = line[i];
                    int sourceIndex = _dragStartEntries.IndexOf(entry);
                    if (sourceIndex < 0 || sourceIndex >= _items.Count)
                        continue;

                    var item = _items[sourceIndex];
                    if (_draggedItem != null && item == _draggedItem)
                        continue;

                    float itemWidth = TextInventoryLayout.MeasureItemWidth(item.ItemName, item.ItemCount) * xUnit;
                    float spacing = i > 0 ? ItemSpacing : 0f;

                    xOffset += spacing;
                    AnimateItemPosition(item, new Vector2(xOffset, yOffset));
                    xOffset += itemWidth;
                }
                yOffset += FontSize + LineSpacing;
            }
        }

        private void AnimateItemPosition(TextInventoryItem item, Vector2 targetPosition)
        {
            if (item == null)
                return;

            if (_positionTweens.TryGetValue(item, out var oldTween))
            {
                oldTween?.Kill();
            }

            var tween = item.CreateTween();
            tween.SetEase(Tween.EaseType.Out);
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(item, "position", targetPosition, InventoryUI.BackpackReorderAnimationDuration);
            _positionTweens[item] = tween;
        }

        private void CancelDragPreview()
        {
            if (!_isDragging)
                return;

            _entries = new List<TextInventoryLayout.ItemEntry>(_dragStartEntries);
            _previewEntries = new List<TextInventoryLayout.ItemEntry>(_dragStartEntries);

            var restoredItems = new List<TextInventoryItem>(_dragStartEntries.Count);
            foreach (var entry in _dragStartEntries)
            {
                var item = _items.Find(i => i.ItemId == entry.ItemId);
                if (item != null)
                    restoredItems.Add(item);
            }
            _items = restoredItems;

            AnimateToPreviewLayout();

            _isDragging = false;
            _draggedItem = null;
            _dragStartEntries.Clear();
            _previewEntries.Clear();
            _dragStartRects.Clear();
            _dragTargetIndex = -1;
            QueueRedraw();
        }

        private void SendReorderItems()
        {
            var manager = UiServices.GetInventoryManager(this);
            if (manager == null)
                return;

            var orderedIds = new List<uint>(_entries.Count);
            foreach (var entry in _entries)
                orderedIds.Add(entry.ItemId);

            manager.SendReorderItems(orderedIds);
        }

        public override void _Draw()
        {
            if (DrawDebugBorder)
            {
                DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.7f, 0.85f, 1f, 0.5f), false, 1f);
            }

            if (_items.Count == 0)
            {
                var font = GetThemeDefaultFont();
                if (font == null) return;
                var textSize = font.GetStringSize("（空）", fontSize: FontSize);
                var ascent = font.GetAscent(FontSize);
                var pos = new Vector2(
                    (Size.X - textSize.X) / 2,
                    Size.Y / 2 + ascent / 2
                );
                DrawString(font, pos, "（空）", HorizontalAlignment.Left, -1, FontSize, new Color(0.4f, 0.4f, 0.4f));
                return;
            }
        }

        [Signal]
        public delegate void ItemRightClickedEventHandler(uint itemId, uint count);

        public override void _Notification(int what)
        {
            if (what == NotificationMouseExit)
            {
                CancelDragPreview();
                _dragTargetIndex = -1;
                QueueRedraw();
            }
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            if (data.AsGodotObject() is not TextInventoryItem draggedItem)
            {
                CancelDragPreview();
                return false;
            }

            if (!_isDragging || _draggedItem != draggedItem)
            {
                _dragStartEntries = new List<TextInventoryLayout.ItemEntry>(_entries);
                _previewEntries = new List<TextInventoryLayout.ItemEntry>(_entries);
                _dragStartRects = new List<Rect2>(_items.Count);
                foreach (var item in _items)
                    _dragStartRects.Add(new Rect2(item.Position, item.Size));
                _isDragging = true;
                _draggedItem = draggedItem;
            }

            int fromIndex = _items.IndexOf(draggedItem);
            if (fromIndex < 0)
            {
                CancelDragPreview();
                return false;
            }

            int insertIndex = CalculateInsertIndex(atPosition);

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

            _dragTargetIndex = insertIndex;
            QueueRedraw();
            return true;
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

        private void OnItemRightClicked(uint itemId, uint count)
        {
            EmitSignal(SignalName.ItemRightClicked, itemId, count);
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (data.AsGodotObject() is not TextInventoryItem draggedItem)
                return;

            int fromIndex = _items.IndexOf(draggedItem);
            if (fromIndex < 0) return;

            int toIndex = CalculateInsertIndex(atPosition);
            if (toIndex == fromIndex || toIndex == fromIndex + 1)
            {
                CancelDragPreview();
                return;
            }

            var entry = _entries[fromIndex];
            _entries.RemoveAt(fromIndex);

            var item = _items[fromIndex];
            _items.RemoveAt(fromIndex);

            int insertIndex = toIndex;
            if (insertIndex > fromIndex)
                insertIndex--;
            _entries.Insert(insertIndex, entry);
            _items.Insert(insertIndex, item);

            CancelPositionTweens();

            SendReorderItems();

            _isDragging = false;
            _draggedItem = null;
            _dragStartEntries.Clear();
            _previewEntries.Clear();
            _dragTargetIndex = -1;
            QueueRedraw();
        }

        private int CalculateInsertIndex(Vector2 position)
        {
            for (int i = 0; i < _dragStartRects.Count; i++)
            {
                var rect = _dragStartRects[i];
                var center = rect.Position + rect.Size / 2;
                if (position.Y < center.Y || (position.Y < center.Y + rect.Size.Y / 2 && position.X < center.X))
                    return i;
            }
            return _items.Count;
        }

        private void CancelPositionTweens()
        {
            foreach (var tween in _positionTweens.Values)
            {
                tween?.Kill();
            }
            _positionTweens.Clear();
        }
    }
}
