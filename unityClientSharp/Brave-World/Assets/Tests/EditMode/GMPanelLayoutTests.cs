using System.Collections.Generic;
using NUnit.Framework;
using UnityClientSharp.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BraveWorld.Tests
{
    /// <summary>
    /// 面板布局高度断言（GMPanel 布局事故回归测试）。
    /// 根因：VerticalLayoutGroup 的 childControlHeight=false 时，uGUI 源码 GetChildSizes
    /// 直接取 child.sizeDelta（运行时新建节点恒为 0），LayoutElement 的 preferred/flexible
    /// 全部失效，内容塌缩。这里实例化面板强制布局，断言关键区域的真实 Rect 高度。
    /// 布局尺寸全部由 Panel 的 sizeDelta 派生（居中锚点），与 batchmode 屏幕尺寸无关。
    /// </summary>
    public class GMPanelLayoutTests
    {
        private static RectTransform FindRect(Component root, string name)
        {
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == name) return rt;
            return null;
        }

        private static List<RectTransform> FindRectsByPrefix(Component root, string prefix)
        {
            var result = new List<RectTransform>();
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                if (rt.name.StartsWith(prefix)) result.Add(rt);
            return result;
        }

        private static void ActivateAndLayout(MonoBehaviour hud, string canvasName)
        {
            // 面板 Build 末尾 SetVisible(false)；直接激活画布（绕过输入框聚焦，EditMode 更稳）
            hud.transform.Find(canvasName).gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }

        [Test]
        public void GMPanel_KeyRegions_HaveExpectedHeights()
        {
            var hud = GMPanelHud.Create();
            try
            {
                ActivateAndLayout(hud, "GMPanelCanvas");

                // 命令栏固定 32 高（事故症状 1：被撑成两行高/塌缩）
                var commandBar = FindRect(hud, "CommandBar");
                Assert.IsNotNull(commandBar, "CommandBar 应存在");
                Assert.AreEqual(32f, commandBar.rect.height, 1f, "命令栏应固定 32 高");

                // 执行按钮与输入框同高
                var execBtn = FindRect(hud, "Exec");
                Assert.AreEqual(32f, execBtn.rect.height, 1f, "执行按钮应与命令栏同高");
                var cmdInput = FindRect(hud, "CmdInput");
                Assert.AreEqual(32f, cmdInput.rect.height, 1f, "输入框应与命令栏同高（forceExpand 泄漏回归）");

                // 响应日志区固定 90 高，不被挤没（事故症状 2）
                var log = FindRect(hud, "ResponseLog");
                Assert.GreaterOrEqual(log.rect.height, 80f, "响应日志区应保留 ≥80 高");

                // 日志行宽度不塌缩（第二轮"竖排单字"根因回归）：注入一行日志后断言宽度
                typeof(GMPanelHud)
                    .GetMethod("AppendLog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(hud, new object[] { "green", "测试日志输出" });
                Canvas.ForceUpdateCanvases();
                var logLines = FindRectsByPrefix(hud, "Log");
                Assert.Greater(logLines.Count, 0, "注入日志后应有日志行");
                Assert.Greater(logLines[0].rect.width, 300f, "日志行宽度不应塌缩（应 ≈ 视口宽）");

                // 分组滚动区弹性吃剩余空间（事故症状 3）
                var scroll = FindRect(hud, "GroupScroll");
                Assert.Greater(scroll.rect.height, 150f, "分组滚动区应占据剩余全部空间");

                // 分组内容真实渲染：出厂默认配置 6 个组头，高度 24；道具组默认展开有按钮行
                var headers = FindRectsByPrefix(hud, "Header_");
                Assert.GreaterOrEqual(headers.Count, 6, "默认分组（含出厂 gm_panel.json）应全部渲染组头");
                Assert.AreEqual(24f, headers[0].rect.height, 1f, "组头行高 24");
                // 组头文本宽度不塌缩（第二轮"组头只剩孤字符"根因回归）
                var nameTexts = FindRectsByPrefix(hud, "Name");
                Assert.Greater(nameTexts.Count, 0, "组头应含 Name 文本");
                Assert.Greater(nameTexts[0].rect.width, 100f, "组头文本宽度不应塌缩（应吃满弹性宽）");
                Assert.LessOrEqual(headers[0].rect.width, 420f, "组头宽度不应溢出面板（100 默认尺寸回归）");
                var flowRows = FindRectsByPrefix(hud, "FlowRow");
                Assert.Greater(flowRows.Count, 0, "道具组默认展开，应至少有一行命令按钮流");
                Assert.AreEqual(26f, flowRows[0].rect.height, 1f, "按钮流行高 26");
                var container = FindRect(hud, "GroupContainer");
                Assert.Greater(container.rect.height, 6 * 24f, "滚动 Content 应自适应内容高度（CSF 生效）");

                // 工具栏与添加分组按钮（事故症状 4：按钮被拉成巨块）
                var toolbar = FindRect(hud, "Toolbar");
                Assert.AreEqual(32f, toolbar.rect.height, 1f, "工具栏应固定 32 高");
                var addBtn = FindRect(hud, "AddGroup");
                Assert.LessOrEqual(addBtn.rect.height, 40f, "添加分组按钮不应被拉高");
                Assert.LessOrEqual(addBtn.rect.width, 120f, "添加分组按钮不应被拉宽");
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        [Test]
        public void CharacterPanel_AttrRows_HaveExpectedHeights()
        {
            var hud = CharacterPanelHud.Create();
            try
            {
                ActivateAndLayout(hud, "CharacterPanelCanvas");

                // 10 行属性行 + 底部按钮行，行高 26 / 30（同类塌缩根因回归）
                var firstRow = FindRect(hud, "Row_hp");
                Assert.IsNotNull(firstRow, "属性行应存在");
                Assert.AreEqual(26f, firstRow.rect.height, 1f, "属性行高 26");
                var bottom = FindRect(hud, "BottomActions");
                Assert.AreEqual(30f, bottom.rect.height, 1f, "底部按钮行高 30");
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        [Test]
        public void SkillPanel_Columns_HeaderAndScrollVisible()
        {
            var hud = SkillPanelHud.Create();
            try
            {
                ActivateAndLayout(hud, "SkillPanelCanvas");

                // 列头 20 高 + 滚动区弹性（同一根因波及 pre-existing 面板）
                var headers = FindRectsByPrefix(hud, "Header");
                Assert.AreEqual(3, headers.Count, "三列各一个列头");
                Assert.AreEqual(20f, headers[0].rect.height, 1f, "列头行高 20");
                var scrolls = FindRectsByPrefix(hud, "Scroll");
                Assert.AreEqual(3, scrolls.Count);
                Assert.Greater(scrolls[0].rect.height, 100f, "滚动列表区应吃满列内剩余空间");
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }
    }
}
