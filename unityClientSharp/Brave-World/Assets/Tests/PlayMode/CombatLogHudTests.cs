using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BraveWorld.Tests.PlayMode
{
    /// <summary>HUD 隔离验证：绕过网络直接喂 CombatLogNotify，文本必须写入。</summary>
    public class CombatLogHudTests
    {
        [UnityTest]
        public IEnumerator Hud_RendersInjectedEntry()
        {
            var hud = UnityClientSharp.UI.CombatLogHud.Create();
            yield return null; // Start 订阅

            var notify = new Game.CombatLogNotify();
            notify.Entries.Add(new Game.CombatLogEntry
            {
                LogType = Game.CombatLogType.CombatLogDamage,
                Timestamp = 1784336400,
                ActorName = "测试勇者",
                TargetName = "史莱姆",
                Extra = "测试勇者 对 史莱姆 造成 15 点伤害",
            });

            var m = typeof(UnityClientSharp.UI.CombatLogHud).GetMethod("OnCombatLog",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(m);
            m.Invoke(hud, new object[] { notify });
            yield return null; // LateUpdate 写入

            var f = typeof(UnityClientSharp.UI.CombatLogHud).GetField("_text",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tmp = (TMPro.TextMeshProUGUI)f.GetValue(hud);
            Debug.Log($"[Diag] HUD text=[{tmp.text}]");
            Assert.IsTrue(tmp.text.Contains("测试勇者"), "HUD 未写入日志文本");

            Object.DestroyImmediate(hud.gameObject);
        }
    }
}
