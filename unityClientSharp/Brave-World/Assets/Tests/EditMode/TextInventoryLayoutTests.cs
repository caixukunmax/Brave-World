using System.Collections.Generic;
using NUnit.Framework;
using UnityClientSharp.UI;

namespace BraveWorld.Tests
{
    /// <summary>
    /// 文本背包布局：测宽公式、流式换行、容量检查（逐行对齐 Godot TextInventoryLayout，
    /// 数值用例取自 docs/design/2026-06-16-text-inventory-panel-design.md 第 2 节）。
    /// </summary>
    public class TextInventoryLayoutTests
    {
        private static TextInventoryLayout.ItemEntry Entry(string name, uint count, uint id = 1)
        {
            return new TextInventoryLayout.ItemEntry { ItemId = id, Name = name, Count = count };
        }

        [Test]
        public void MeasureItemWidth_MatchesDesignDoc()
        {
            // 设计文档示例：魔法剑×1 = 3 + 1 + 1×0.6 = 4.6
            Assert.AreEqual(4.6f, TextInventoryLayout.MeasureItemWidth("魔法剑", 1), 1e-4f);
            // 高精宝石×30 = 4 + 1 + 2×0.6 = 6.2
            Assert.AreEqual(6.2f, TextInventoryLayout.MeasureItemWidth("高精宝石", 30), 1e-4f);
            // 金币×9999 = 2 + 1 + 4×0.6 = 5.4
            // （设计文档示例表该行的合计列笔误写成 6.4；公式与 Godot/服务器实现的结果为 5.4，以公式为准）
            Assert.AreEqual(5.4f, TextInventoryLayout.MeasureItemWidth("金币", 9999), 1e-4f);
        }

        [Test]
        public void MeasureItemWidth_ZeroCountCountsAsOneDigit()
        {
            // count=0 保留显示为 "×0"，按 1 位数计宽
            Assert.AreEqual(2.6f, TextInventoryLayout.MeasureItemWidth("弓", 0), 1e-4f);
        }

        [Test]
        public void Reflow_KeepsItemsOnSameLineWhileTheyFit()
        {
            var items = new List<TextInventoryLayout.ItemEntry> { Entry("魔法剑", 1, 1), Entry("魔法剑", 1, 2) };
            var lines = TextInventoryLayout.Reflow(items, 30);
            // 4.6 + 0.5(间距) + 4.6 = 9.7 ≤ 30，同行
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(2, lines[0].Count);
        }

        [Test]
        public void Reflow_WrapsWhenLineWouldOverflow()
        {
            // 每条 8 个汉字 + ×1 = 9.6x；30 宽每行最多 3 条（9.6×3+0.5×2=29.8），第 4 条换行
            var items = new List<TextInventoryLayout.ItemEntry>
            {
                Entry("一二三四五六七八", 1, 1), Entry("一二三四五六七八", 1, 2),
                Entry("一二三四五六七八", 1, 3), Entry("一二三四五六七八", 1, 4),
            };
            var lines = TextInventoryLayout.Reflow(items, 30);
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(3, lines[0].Count);
            Assert.AreEqual(1, lines[1].Count);
        }

        [Test]
        public void Reflow_ItemWiderThanLineThrows()
        {
            var items = new List<TextInventoryLayout.ItemEntry> { Entry("一二三四五六七八九十", 1) }; // 11.6x > 10
            Assert.Throws<System.InvalidOperationException>(() => TextInventoryLayout.Reflow(items, 10));
        }

        [Test]
        public void CanFit_ChecksLineCountAndWidth()
        {
            var items = new List<TextInventoryLayout.ItemEntry> { Entry("魔法剑", 1, 1), Entry("金币", 99, 2) };
            Assert.IsTrue(TextInventoryLayout.CanFit(items, 30, 10));
            Assert.IsFalse(TextInventoryLayout.CanFit(items, 30, 0), "行数为 0 放不下任何条目");
            Assert.IsFalse(TextInventoryLayout.CanFit(items, 3, 10), "单条超宽时放不下");
        }
    }
}
