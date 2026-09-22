using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityClientSharp.Entity;
using UnityClientSharp.Net;
using UnityClientSharp.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace BraveWorld.Tests.PlayMode
{
    /// <summary>SkillPanel PlayMode 验证：三列填充、选中详情、显隐切换（离线注入缓存）。</summary>
    public class SkillPanelHudTests
    {
        private GameObject _nmGo;
        private NetworkManager _nm;
        private SkillPanelHud _hud;

        private IEnumerator SetupHud()
        {
            _nmGo = new GameObject("NM");
            _nm = _nmGo.AddComponent<NetworkManager>(); // Awake 设 Instance
            _nm.CachedLearnedSkills = new List<uint> { 2, 3, 5 };
            _nm.CachedEquippedSkills = new List<uint> { 2, 0, 0, 0 };
            _nm.CachedRoleInfo = new Game.FullRoleInfo { Job = "战士" };
            _hud = SkillPanelHud.Create();
            yield return null; // Start 订阅
        }

        private IEnumerator Teardown()
        {
            if (_hud != null) Object.Destroy(_hud.gameObject);
            if (_nmGo != null) Object.Destroy(_nmGo);
            yield return null;
        }

        private static string RowNameText(Transform row) =>
            row.Find("Name/Text").GetComponent<TMPro.TextMeshProUGUI>().text;

        [UnityTest]
        public IEnumerator Panel_PopulatesThreeColumns()
        {
            yield return SetupHud();

            // 装备列固定 4 行：[0] 烈斩（带 - 钮）、其余空槽（无操作钮）
            Assert.AreEqual(4, _hud.EquippedContent.childCount);
            var slot0 = _hud.EquippedContent.GetChild(0);
            StringAssert.Contains("烈斩", RowNameText(slot0));
            Assert.NotNull(slot0.Find("Action"), "非空槽应有 - 钮");
            var slot1 = _hud.EquippedContent.GetChild(1);
            StringAssert.Contains("[1]", RowNameText(slot1));
            Assert.IsNull(slot1.Find("Action"), "空槽不得有操作钮");

            // 已学习列 = 已学未装备（3,5；2 已装备被排除）
            Assert.AreEqual(2, _hud.LearnedContent.childCount);
            StringAssert.Contains("盾击", RowNameText(_hud.LearnedContent.GetChild(0)));
            Assert.NotNull(_hud.LearnedContent.GetChild(0).Find("Action"), "已学习行应有 + 钮");

            // 可学习列 = 本职业（战士 job1）排除已学（数据驱动：JSON 每职业 6 个技能，不写死）
            var expected = SkillDataUtil.GetLearnableIdsForJob(1).FindAll(id => !_nm.CachedLearnedSkills.Contains(id));
            Assert.AreEqual(expected.Count, _hud.LearnableContent.childCount);
            if (expected.Count > 0)
            {
                StringAssert.Contains(SkillDataUtil.GetName(expected[0]), RowNameText(_hud.LearnableContent.GetChild(0)));
                Assert.NotNull(_hud.LearnableContent.GetChild(0).Find("Action"), "可学习行应有 L 钮");
            }

            // 详情默认提示
            StringAssert.Contains("选择一个技能", _hud.Detail.text);

            // 默认隐藏
            Assert.IsFalse(_hud.PanelVisible);

            yield return Teardown();
        }

        [UnityTest]
        public IEnumerator SelectSkill_UpdatesDetail()
        {
            yield return SetupHud();

            var nameBtn = _hud.LearnedContent.GetChild(0).Find("Name").GetComponent<UnityEngine.UI.Button>();
            nameBtn.onClick.Invoke();
            StringAssert.Contains("盾击", _hud.Detail.text);
            StringAssert.Contains("CD:", _hud.Detail.text);
            Assert.AreEqual(3u, _hud.SelectedSkillId);

            yield return Teardown();
        }

        [UnityTest]
        public IEnumerator Toggle_ShowHide_Idempotent()
        {
            yield return SetupHud();

            Assert.IsFalse(_hud.PanelVisible);
            _hud.Toggle();
            Assert.IsTrue(_hud.PanelVisible);
            _hud.Toggle();
            Assert.IsFalse(_hud.PanelVisible);
            _hud.SetVisible(true);
            Assert.IsTrue(_hud.PanelVisible);
            _hud.SetVisible(true);
            Assert.IsTrue(_hud.PanelVisible, "重复 SetVisible(true) 幂等");

            yield return Teardown();
        }
    }
}
