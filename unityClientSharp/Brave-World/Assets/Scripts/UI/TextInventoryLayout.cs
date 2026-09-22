using System;
using System.Collections.Generic;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 文本背包布局计算（纯逻辑，无引擎依赖）。
    /// 逐行移植自 clinetcsharp/Scripts/TextInventoryLayout.cs：
    /// 字符宽度表（汉字 1x / 乘号 1x / 数字 0.6x / 间距 0.5x）、单条测宽、流式换行、容量检查。
    /// 设计文档：docs/design/2026-06-16-text-inventory-panel-design.md。
    /// </summary>
    public static class TextInventoryLayout
    {
        public const float ChineseCharWidth = 1.0f;
        public const float MultiplierWidth = 1.0f;
        public const float DigitWidth = 0.6f;
        public const float SpacingWidth = 0.5f;

        public class ItemEntry
        {
            public uint ItemId { get; set; }
            public string Name { get; set; } = "";
            public uint Count { get; set; }
            public int Quality { get; set; }
        }

        public static float MeasureItemWidth(string name, uint count)
        {
            // 注：本引擎 netstandard 2.1 shim 无 ArgumentNullException.ThrowIfNull，手写判空
            if (name == null) throw new ArgumentNullException(nameof(name));

            // Count is unsigned; zero is reserved for "no item" display as "×0".
            int digits = count == 0 ? 1 : (int)Math.Floor(Math.Log10(count)) + 1;
            return name.Length * ChineseCharWidth + MultiplierWidth + digits * DigitWidth;
        }

        public static List<List<ItemEntry>> Reflow(List<ItemEntry> items, float lineWidth, float spacingWidth = SpacingWidth)
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
                    itemWidth += spacingWidth;

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

        public static bool CanFit(List<ItemEntry> items, float lineWidth, int lineCount, float spacingWidth = SpacingWidth)
        {
            try
            {
                var lines = Reflow(items, lineWidth, spacingWidth);
                return lines.Count <= lineCount;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
