using System.Collections.Generic;
using NUnit.Framework;
using UnityClientSharp.Entity;
using UnityClientSharp.UI;

namespace BraveWorld.Tests
{
    /// <summary>SkillPanel 数据层：可学列表过滤、职业映射、空槽与普攻显示规则（对齐 Godot）。</summary>
    public class SkillPanelTests
    {
        [Test]
        public void GetLearnableIdsForJob_FiltersJob_AndExcludesBasicAndMonster()
        {
            var job1 = SkillDataUtil.GetLearnableIdsForJob(1);
            Assert.IsNotEmpty(job1);
            foreach (var id in job1)
            {
                Assert.Greater(id, 1u, "不得包含普攻 id=1");
                Assert.AreEqual(1, SkillDataUtil.Get(id).job, "只含本职业");
            }
            Assert.IsTrue(job1.Count > 0);
            // 排序（对齐 Godot result.Sort()）
            var sorted = new List<uint>(job1);
            sorted.Sort();
            Assert.AreEqual(sorted, new List<uint>(job1));
        }

        [Test]
        public void GetAllLearnableIds_ExcludesBasicAndMonster()
        {
            var all = SkillDataUtil.GetAllLearnableIds();
            Assert.IsNotEmpty(all);
            foreach (var id in all)
            {
                Assert.Greater(id, 1u);
                Assert.AreNotEqual(4, SkillDataUtil.Get(id).job, "不得包含怪物技能 job=4");
            }
        }

        [Test]
        public void JobNameToId_Mapping()
        {
            Assert.AreEqual(1, SkillDataUtil.JobNameToId("战士"));
            Assert.AreEqual(2, SkillDataUtil.JobNameToId("法师"));
            Assert.AreEqual(3, SkillDataUtil.JobNameToId("牧师"));
            Assert.AreEqual(0, SkillDataUtil.JobNameToId("不存在"));
            Assert.AreEqual(0, SkillDataUtil.JobNameToId(""));
        }

        [Test]
        public void DisplaySkillId_BasicAttackForcedEmpty()
        {
            Assert.AreEqual(0u, SkillPanelHud.DisplaySkillId(1u), "普攻 id=1 强制显示空槽");
            Assert.AreEqual(0u, SkillPanelHud.DisplaySkillId(0u));
            Assert.AreEqual(5u, SkillPanelHud.DisplaySkillId(5u));
        }

        [Test]
        public void FindFirstEmptySlot_Scans()
        {
            Assert.AreEqual(0, SkillPanelHud.FindFirstEmptySlot(new List<uint>()));
            Assert.AreEqual(1, SkillPanelHud.FindFirstEmptySlot(new List<uint> { 2, 0, 3, 4 }));
            Assert.AreEqual(2, SkillPanelHud.FindFirstEmptySlot(new List<uint> { 2, 3 }));
            Assert.AreEqual(-1, SkillPanelHud.FindFirstEmptySlot(new List<uint> { 2, 3, 4, 5 }));
        }
    }
}
