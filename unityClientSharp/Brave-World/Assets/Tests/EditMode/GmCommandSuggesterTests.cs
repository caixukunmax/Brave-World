using System.Collections.Generic;
using NUnit.Framework;
using UnityClientSharp.UI;

namespace BraveWorld.Tests
{
    /// <summary>
    /// GM 自动补全匹配规则（docs/design/gm-panel-autocomplete.md §4）与参数提示。
    /// 注意：设计文档 §7 验收第 2 条（输入 1001 显示 additem,1001,1）与已实现的分层策略
    /// （无逗号输入只按命令名聚合）不一致，以 Godot 实现代码为准——此处测试按代码行为断言。
    /// </summary>
    public class GmCommandSuggesterTests
    {
        private static List<GmCommandSuggester.CommandEntry> DefaultCommands()
        {
            // 取自 Godot AddDefaultGroups 的代表性子集
            return new List<GmCommandSuggester.CommandEntry>
            {
                new("药水x1", "additem,1001,1", ""),
                new("药水x10", "additem,1001,10", ""),
                new("回出生点", "return", ""),
                new("传送坐标(25,25)", "teleport,25,25", ""),
                new("添加宝箱1类(20,20)", "addchest,1,20,20", ""),
                new("学习烈斩(2)", "learnskill,2", ""),
                new("中毒(1)", "addbuff,1", ""),
                new("冰冻(2)", "addbuff,2", ""),
                new("移除中毒", "removebuff,1", ""),
                new("测试道具x10", "addtestitems,10", ""),
            };
        }

        [Test]
        public void GetCommandName_SplitsAtFirstComma()
        {
            Assert.AreEqual("additem", GmCommandSuggester.GetCommandName("additem,1001,1"));
            Assert.AreEqual("return", GmCommandSuggester.GetCommandName("return"));
        }

        [Test]
        public void NoCommaInput_AggregatesByCommandName()
        {
            var suggestions = GmCommandSuggester.CollectSuggestions("addbuf", DefaultCommands());
            // 设计文档示例：addbuf → addbuff,（命令名聚合，有参数自动补逗号）
            Assert.AreEqual(1, suggestions.Count);
            Assert.AreEqual("addbuff,", suggestions[0].InsertText);
            Assert.IsTrue(suggestions[0].DisplayText.StartsWith("addbuff "));
        }

        [Test]
        public void NoCommaInput_PrefixBeforeContains()
        {
            var suggestions = GmCommandSuggester.CollectSuggestions("add", DefaultCommands());
            Assert.IsNotEmpty(suggestions);
            // 前缀匹配（addbuff/addchest/additem/addtestitems）全部排在包含匹配之前
            foreach (var s in suggestions)
                Assert.IsTrue(s.InsertText.StartsWith("add"), $"前缀匹配优先: {s.InsertText}");
        }

        [Test]
        public void FullCommandNameWithComma_MatchesArgVariants()
        {
            var suggestions = GmCommandSuggester.CollectSuggestions("addbuff,", DefaultCommands());
            // 设计文档示例：addbuff, → addbuff,1、addbuff,2 等具体变体
            Assert.AreEqual(2, suggestions.Count);
            foreach (var s in suggestions)
                Assert.IsTrue(s.InsertText.StartsWith("addbuff,"));
        }

        [Test]
        public void PartialNameWithComma_FallsBackToNameAggregation()
        {
            var suggestions = GmCommandSuggester.CollectSuggestions("add,", DefaultCommands());
            // 设计文档示例：add, 不是完整命令名 → 仅显示 addbuff,、addchest,、additem, 等原始命令名
            Assert.IsNotEmpty(suggestions);
            foreach (var s in suggestions)
            {
                StringAssert.StartsWith("add", s.InsertText);
                StringAssert.DoesNotContain("1001", s.InsertText, "不应展开为参数变体");
            }
        }

        [Test]
        public void NameWithoutArgs_InsertsWithoutComma()
        {
            var suggestions = GmCommandSuggester.CollectSuggestions("retu", DefaultCommands());
            Assert.AreEqual(1, suggestions.Count);
            Assert.AreEqual("return", suggestions[0].InsertText, "无参数命令名不补逗号");
        }

        [Test]
        public void Suggestions_CappedAtEight()
        {
            var commands = new List<GmCommandSuggester.CommandEntry>();
            for (int i = 0; i < 20; i++)
                commands.Add(new GmCommandSuggester.CommandEntry($"c{i}", $"cmd{i},1", ""));
            var suggestions = GmCommandSuggester.CollectSuggestions("cmd", commands);
            Assert.AreEqual(GmCommandSuggester.MaxSuggestions, suggestions.Count);
        }

        [Test]
        public void ParamHint_FollowsCommaPosition()
        {
            var commands = DefaultCommands();
            // 依赖 StreamingAssets/Data/gm_command_desc.json（additem → itemid,数量；addbuff → buffid）
            Assert.AreEqual("itemid", GmCommandSuggester.GetParamHintAt("additem,", commands));
            Assert.AreEqual("数量", GmCommandSuggester.GetParamHintAt("additem,1001,", commands));
            Assert.AreEqual("buffid", GmCommandSuggester.GetParamHintAt("addbuff,", commands));
        }

        [Test]
        public void ParamHint_ReturnsNullWhenNotApplicable()
        {
            var commands = DefaultCommands();
            Assert.IsNull(GmCommandSuggester.GetParamHintAt("additem", commands), "未输入逗号");
            Assert.IsNull(GmCommandSuggester.GetParamHintAt("foo,", commands), "非完整命令名");
            Assert.IsNull(GmCommandSuggester.GetParamHintAt("additem,1,2,3,", commands), "超出参数个数");
        }
    }
}
