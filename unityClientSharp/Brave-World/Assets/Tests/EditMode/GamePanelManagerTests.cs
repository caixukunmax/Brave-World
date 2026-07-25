using NUnit.Framework;
using UnityClientSharp.UI;
using UnityEngine;

namespace BraveWorld.Tests
{
    /// <summary>
    /// GamePanelManager 面板栈/查询逻辑 + CharacterPanelHud 属性定义表与取值。
    /// EditMode 下 MonoBehaviour 的 Awake/Update 不执行（AGENTS.md），故只测
    /// TogglePanel/CloseTopmost/GetPanel 等直接可调用逻辑；热键分发（Update）不在此覆盖。
    /// </summary>
    public class GamePanelManagerTests
    {
        private class FakePanel : IGamePanel
        {
            public string PanelName => "fake";
            public bool PanelVisible { get; private set; }
            public void SetVisible(bool visible) => PanelVisible = visible;
        }

        private GamePanelManager _mgr;

        [SetUp]
        public void SetUp()
        {
            _mgr = GamePanelManager.Ensure();
        }

        [TearDown]
        public void TearDown()
        {
            if (_mgr != null)
                Object.DestroyImmediate(_mgr.gameObject);
        }

        [Test]
        public void TogglePanel_TogglesVisibility()
        {
            var panel = new FakePanel();
            _mgr.TogglePanel(panel);
            Assert.IsTrue(panel.PanelVisible);
            _mgr.TogglePanel(panel);
            Assert.IsFalse(panel.PanelVisible);
        }

        [Test]
        public void CloseTopmost_ClosesMostRecentlyShownFirst()
        {
            var a = new FakePanel();
            var b = new FakePanel();
            _mgr.TogglePanel(a);
            _mgr.TogglePanel(b);

            Assert.IsTrue(_mgr.CloseTopmost(), "有可见面板时应关闭成功");
            Assert.IsTrue(a.PanelVisible, "先开的 a 应仍可见");
            Assert.IsFalse(b.PanelVisible, "后开的 b 应被 ESC 关闭");

            Assert.IsTrue(_mgr.CloseTopmost());
            Assert.IsFalse(a.PanelVisible);
            Assert.IsFalse(_mgr.CloseTopmost(), "全部关闭后应返回 false");
        }

        [Test]
        public void CloseTopmost_SkipsPanelsHiddenByOtherMeans()
        {
            var a = new FakePanel();
            var b = new FakePanel();
            _mgr.TogglePanel(a);
            _mgr.TogglePanel(b);
            b.SetVisible(false); // 绕过管理器直接隐藏（如 X 关闭按钮）

            Assert.IsTrue(_mgr.CloseTopmost());
            Assert.IsFalse(a.PanelVisible, "应跳过已隐藏的 b，关到 a");
        }

        [Test]
        public void GetPanel_ReturnsRegisteredByType()
        {
            var panel = new FakePanel();
            _mgr.RegisterPanel(panel);
            Assert.AreSame(panel, _mgr.GetPanel<FakePanel>());
            _mgr.UnregisterPanel(panel);
            Assert.IsNull(_mgr.GetPanel<FakePanel>());
        }

        private class EscapeAwarePanel : IGamePanel, IGamePanelEscapeHandler
        {
            public string PanelName => "escape-aware";
            public bool PanelVisible { get; private set; }
            public bool ConsumeEscape;
            public void SetVisible(bool visible) => PanelVisible = visible;
            public bool OnEscape() => ConsumeEscape;
        }

        [Test]
        public void HandleEscape_PanelConsumesEscape_PanelStaysOpen()
        {
            var panel = new EscapeAwarePanel { ConsumeEscape = true };
            _mgr.TogglePanel(panel);
            _mgr.HandleEscape();
            Assert.IsTrue(panel.PanelVisible, "面板消费 ESC 时不应被关闭");
        }

        [Test]
        public void HandleEscape_PanelDoesNotConsume_ClosesPanel()
        {
            var panel = new EscapeAwarePanel { ConsumeEscape = false };
            _mgr.TogglePanel(panel);
            _mgr.HandleEscape();
            Assert.IsFalse(panel.PanelVisible, "面板不消费 ESC 时应被关闭");
        }

        [Test]
        public void AttrDefs_MatchGodotTable()
        {
            // 逐行对齐 Godot CharacterPanel.Data.AttrDefs
            var expected = new (uint key, string label, string gm, int min, int max)[]
            {
                (1, "HP", "hp", 0, 99999),
                (2, "MaxHP", "max_hp", 1, 99999),
                (3, "MP", "mp", 0, 99999),
                (4, "MaxMP", "max_mp", 1, 99999),
                (6, "物攻", "patk", 0, 99999),
                (7, "魔攻", "matk", 0, 99999),
                (8, "物防", "pdef", 0, 99999),
                (9, "魔防", "mdef", 0, 99999),
                (10, "移速", "move_speed", 120, 600),
                (11, "MP恢复/秒", "mp_regen", 0, 99999),
            };
            Assert.AreEqual(expected.Length, CharacterPanelHud.AttrDefs.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var def = CharacterPanelHud.AttrDefs[i];
                Assert.AreEqual(expected[i].key, def.Key, $"第{i}行 key");
                Assert.AreEqual(expected[i].label, def.Label, $"第{i}行 label");
                Assert.AreEqual(expected[i].gm, def.GmName, $"第{i}行 gmName");
                Assert.AreEqual(expected[i].min, def.MinValue, $"第{i}行 min");
                Assert.AreEqual(expected[i].max, def.MaxValue, $"第{i}行 max");
            }
        }

        [Test]
        public void TryGetAttr_FindsValueOrReturnsFalse()
        {
            var roleInfo = new Game.FullRoleInfo();
            roleInfo.Attrs.Add(new Game.AttrItem { Key = 2, Value = 500 });
            Assert.IsTrue(CharacterPanelHud.TryGetAttr(roleInfo, 2, out var v));
            Assert.AreEqual(500, v);
            Assert.IsFalse(CharacterPanelHud.TryGetAttr(roleInfo, 99, out _));
            Assert.IsFalse(CharacterPanelHud.TryGetAttr(null, 2, out _));
        }
    }
}
