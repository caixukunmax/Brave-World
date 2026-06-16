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
            int used = 0;
            foreach (var entry in _entries)
            {
                used += (int)Mathf.Ceil(TextInventoryLayout.MeasureItemWidth(entry.Name, entry.Count));
            }
            return (used, total);
        }

        private void Rebuild()
        {
            foreach (var child in _items)
                child.QueueFree();
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
            float xOffset = 0f;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                float itemWidth = TextInventoryLayout.MeasureItemWidth(item.ItemName, item.ItemCount) * xUnit;
                float spacing = i > 0 ? GetSpacingPixels() : 0f;

                if (xOffset + spacing + itemWidth > LineWidth * xUnit && xOffset > 0)
                {
                    xOffset = 0f;
                    yOffset += FontSize + LineSpacing;
                }

                xOffset += spacing;
                item.Position = new Vector2(xOffset, yOffset);
                item.Size = new Vector2(itemWidth, FontSize);
                xOffset += itemWidth;
            }
        }

        private float GetSpacingPixels()
        {
            return FontSize * 0.5f;
        }

        public override void _Draw()
        {
            if (_dragTargetIndex >= 0 && _dragTargetIndex <= _items.Count)
            {
                float y = GetInsertY(_dragTargetIndex);
                DrawLine(new Vector2(0, y), new Vector2(Size.X, y), Colors.Yellow, 2f);
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

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            if (data.VariantType != Variant.Type.Object || data.AsGodotObject() is not TextInventoryItem)
                return false;

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
                if (position.Y < item.Position.Y + item.Size.Y / 2)
                    return i;
            }
            return _items.Count;
        }
    }
}
