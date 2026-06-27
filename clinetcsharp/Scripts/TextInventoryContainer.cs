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

        private float GetInsertY(int index)
        {
            if (index <= 0) return 0f;
            if (index >= _items.Count) return _items[_items.Count - 1].Position.Y + FontSize;
            return _items[index].Position.Y;
        }

        [Signal]
        public delegate void ItemRightClickedEventHandler(uint itemId, uint count);

        public override void _Notification(int what)
        {
            if (what == NotificationMouseExit)
            {
                _dragTargetIndex = -1;
                QueueRedraw();
            }
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            if (data.VariantType != Variant.Type.Object || data.AsGodotObject() is not TextInventoryItem)
            {
                _dragTargetIndex = -1;
                QueueRedraw();
                return false;
            }

            _dragTargetIndex = CalculateInsertIndex(atPosition);
            QueueRedraw();
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
            if (toIndex == fromIndex || toIndex == fromIndex + 1) return;

            var entry = _entries[fromIndex];
            _entries.RemoveAt(fromIndex);
            if (toIndex > fromIndex) toIndex--;
            _entries.Insert(toIndex, entry);

            Rebuild();
            _dragTargetIndex = -1;
            QueueRedraw();
        }

        private int CalculateInsertIndex(Vector2 position)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var center = item.Position + item.Size / 2;
                if (position.Y < center.Y || (position.Y < center.Y + item.Size.Y / 2 && position.X < center.X))
                    return i;
            }
            return _items.Count;
        }
    }
}
