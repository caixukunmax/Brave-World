using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityClientSharp.Entity;
using UnityEngine;
using UnityEngine.TestTools;

namespace BraveWorld.Tests.PlayMode
{
    /// <summary>NPC 交互菜单 PlayMode 验证：幂等开关、挑战失败回滚血蓝条。</summary>
    public class NpcInteractPlayModeTests
    {
        [UnityTest]
        public IEnumerator Menu_ShowClose_Idempotent()
        {
            var mgrGo = new GameObject("NpcMgr");
            var mgr = mgrGo.AddComponent<NpcManager>();
            mgr.GridSize = 111;
            yield return null; // Awake/Start 跑完（无 NetworkManager → 跳过订阅）

            mgr.SpawnNpcs(new List<Game.NpcInfo>
            {
                new Game.NpcInfo
                {
                    NpcInstanceId = 1, NpcName = "转职大师", NpcType = 1,
                    X = 5, Y = 5, SizeX = 1, SizeY = 1, Direction = 1,
                },
            });
            var npc = mgr.GetNpcs().First();

            mgr.ShowInteractMenu(npc, new Vector2Int(5, 4));
            Assert.NotNull(npc.transform.Find("NpcInteractMenu"), "菜单应挂在 NPC 下");

            // 双触发幂等：先关再建（Destroy 是帧末延迟销毁，与 Godot QueueFree 同语义），帧末后 NPC 下只留一个菜单
            mgr.ShowInteractMenu(npc, new Vector2Int(5, 4));
            yield return null;
            int count = 0;
            foreach (Transform child in npc.transform)
                if (child.name == "NpcInteractMenu") count++;
            Assert.AreEqual(1, count, "重复 Show 不得残留多个菜单");

            mgr.CloseInteractMenu();
            yield return null;
            Assert.IsNull(npc.transform.Find("NpcInteractMenu"), "关闭后菜单应销毁");

            Object.Destroy(mgrGo);
        }

        [UnityTest]
        public IEnumerator CombatResponse_Failure_RollsBackBars()
        {
            var mgrGo = new GameObject("NpcMgr");
            var mgr = mgrGo.AddComponent<NpcManager>();
            mgr.GridSize = 111;
            yield return null;

            mgr.SpawnNpcs(new List<Game.NpcInfo>
            {
                new Game.NpcInfo
                {
                    NpcInstanceId = 7, NpcName = "挑战者", NpcType = 2,
                    X = 5, Y = 5, SizeX = 1, SizeY = 1, Direction = 1,
                },
            });
            var npc = mgr.GetNpcs().First();

            // 乐观亮条
            mgr.TriggerNpcCombat(npc);
            Assert.IsTrue(npc.transform.Find("HpBar").gameObject.activeSelf);

            // 404 失败（反射调私有处理器，与 CombatLogHudTests 同模式）
            var rsp = new Game.NpcCombatResponse
            {
                Code = Common.ErrorCode.InvalidRequest,
                NpcInstanceId = 7,
            };
            typeof(NpcManager)
                .GetMethod("OnNpcCombatResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(mgr, new object[] { rsp });

            Assert.IsFalse(npc.transform.Find("HpBar").gameObject.activeSelf);
            Assert.IsFalse(npc.transform.Find("MpBar").gameObject.activeSelf);

            Object.Destroy(mgrGo);
        }
    }
}
