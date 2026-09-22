using NUnit.Framework;
using UnityClientSharp.Entity;
using UnityClientSharp.UI;
using UnityEngine;

namespace BraveWorld.Tests
{
    /// <summary>战斗同步阶段测试：色表/格式/默认值/组件。</summary>
    public class CombatTests
    {
        [Test]
        public void CastBarDefault_MatchesGodot()
        {
            var cast = BarData.CreateCastBarDefault();
            Assert.AreEqual(-80f, cast.OffsetY);
            Assert.AreEqual(60f / 111f, cast.LengthScale, 1e-4f);
            Assert.AreEqual(4f / 111f, cast.HeightScale, 1e-4f);
            Assert.AreEqual(new Color(0.3f, 0.5f, 1f), cast.Color);
            Assert.AreEqual(0f, cast.FillPercent);
        }

        [Test]
        public void PlayerAndMonsterProfiles_HaveCastBar()
        {
            Assert.NotNull(EntityProfileManager.GetProfile(1).GetData<BarData>("castbar"));
            Assert.NotNull(EntityProfileManager.GetProfile(2).GetData<BarData>("castbar"));
        }

        [Test]
        public void BuffColor_Table()
        {
            Assert.AreEqual(new Color(0.6f, 0.2f, 0.8f), BuffBarHud.GetBuffColor(1));  // 中毒紫
            Assert.AreEqual(new Color(0.3f, 0.6f, 1f), BuffBarHud.GetBuffColor(2));    // 冰冻蓝
            Assert.AreEqual(new Color(1f, 0.3f, 0.1f), BuffBarHud.GetBuffColor(11));   // 狂暴深红
            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f), BuffBarHud.GetBuffColor(999)); // 未知灰
        }

        [Test]
        public void CombatLogColor_Table()
        {
            Assert.AreEqual("#FFD700", CombatLogHud.LogTypeColor(Game.CombatLogType.CombatLogStart));
            Assert.AreEqual("#FFA500", CombatLogHud.LogTypeColor(Game.CombatLogType.CombatLogDamage));
            Assert.AreEqual("#FF0000", CombatLogHud.LogTypeColor(Game.CombatLogType.CombatLogDeath));
            Assert.AreEqual("#FFFFFF", CombatLogHud.LogTypeColor(Game.CombatLogType.CombatLogBuffApply));
        }

        [Test]
        public void SkillDataUtil_FallbackAndGet()
        {
            // id 1 普通攻击（fallback 或 JSON 覆盖后都应存在且数值语义一致）
            var s = SkillDataUtil.Get(1);
            Assert.AreEqual("普通攻击", s.name);
            Assert.AreEqual(1, s.range);
            Assert.AreEqual(0.3, s.castTime, 1e-6);
            Assert.AreEqual(1.5, s.cd, 1e-6);

            // 未知 id 回退
            Assert.AreEqual("技能9999", SkillDataUtil.GetName(9999));
            var unknown = SkillDataUtil.Get(9999);
            Assert.IsTrue(unknown.name.Contains("未知技能"));
        }

        [Test]
        public void SkillIconCatalog_PaletteAndSprite()
        {
            var spec = SkillIconCatalog.Get(5); // 火球术
            ColorUtility.TryParseHtmlString("#5B1D12", out var expectedBg);
            Assert.AreEqual(expectedBg, spec.Background);

            var def = SkillIconCatalog.Get(9999); // 未知走默认
            ColorUtility.TryParseHtmlString("#253046", out var expectedDefBg);
            Assert.AreEqual(expectedDefBg, def.Background);

            Assert.NotNull(SkillIconCatalog.GetSprite(5));
        }
    }
}
