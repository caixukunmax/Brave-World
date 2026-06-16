using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    public static class TextInventoryLayout
    {
        public const float ChineseCharWidth = 1.0f;
        public const float MultiplierWidth = 1.0f;
        public const float DigitWidth = 0.6f;

        public class ItemEntry
        {
            public uint ItemId;
            public string Name = "";
            public uint Count;
            public int Quality;
        }

        public static float MeasureItemWidth(string name, uint count)
        {
            int digits = count <= 0 ? 1 : (int)Math.Floor(Math.Log10(count)) + 1;
            return name.Length * ChineseCharWidth + MultiplierWidth + digits * DigitWidth;
        }

        public static List<List<ItemEntry>> Reflow(List<ItemEntry> items, float lineWidth)
        {
            var lines = new List<List<ItemEntry>>();
            var currentLine = new List<ItemEntry>();
            float currentWidth = 0f;

            foreach (var item in items)
            {
                float itemWidth = MeasureItemWidth(item.Name, item.Count);
                if (itemWidth > lineWidth)
                    throw new InvalidOperationException($"Item '{item.Name}' is too wide for the inventory panel.");

                if (currentLine.Count > 0)
                    itemWidth += GetSpacingWidth();

                if (currentWidth + itemWidth > lineWidth)
                {
                    lines.Add(currentLine);
                    currentLine = new List<ItemEntry>();
                    currentWidth = 0f;
                    itemWidth = MeasureItemWidth(item.Name, item.Count);
                }

                currentLine.Add(item);
                currentWidth += itemWidth;
            }

            if (currentLine.Count > 0)
                lines.Add(currentLine);

            return lines;
        }

        public static bool CanFit(List<ItemEntry> items, float lineWidth, int lineCount)
        {
            try
            {
                var lines = Reflow(items, lineWidth);
                return lines.Count <= lineCount;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static float GetSpacingWidth()
        {
            return 0.5f;
        }
    }
}
