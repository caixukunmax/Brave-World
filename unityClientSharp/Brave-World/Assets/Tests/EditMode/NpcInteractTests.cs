using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityClientSharp.Entity;
using UnityEngine;

namespace BraveWorld.Tests
{
    /// <summary>NPC 交互：配置表、四邻格检测、footprint 阻挡、挑战乐观显示（对齐 Godot NpcManager）。</summary>
    public class NpcInteractTests
    {
        private static NpcManager CreateMgrWithNpc(ulong id, int x, int y, int sizeX = 1, int sizeY = 1, int type = 1)
        {
            var go = new GameObject("NpcMgr");
            var mgr = go.AddComponent<NpcManager>();
            mgr.GridSize = 111;
            mgr.SpawnNpcs(new List<Game.NpcInfo>
            {
                new Game.NpcInfo
                {
                    NpcInstanceId = id, NpcName = "测试NPC", NpcType = type,
                    X = x, Y = y, SizeX = sizeX, SizeY = sizeY, Direction = 1,
                },
            });
            return mgr;
        }

        [Test]
        public void InteractConfig_JobMaster_HasDialogAndChangeJob()
        {
            var options = NpcManager.GetInteractOptions(NpcType.JobMaster);
            Assert.NotNull(options);
            Assert.AreEqual(2, options.Length);
            Assert.AreEqual("对话", options[0].Label);
            Assert.IsNotEmpty(options[0].DialogText);
            Assert.AreEqual("转职", options[1].Label);
            Assert.IsTrue(options[1].ShowChangeJob);
            Assert.IsFalse(options[1].TriggerCombat);
        }

        [Test]
        public void InteractConfig_Combatant_HasDialogAndCombat()
        {
            var options = NpcManager.GetInteractOptions(NpcType.Combatant);
            Assert.NotNull(options);
            Assert.AreEqual(2, options.Length);
            Assert.AreEqual("对话", options[0].Label);
            Assert.IsNotEmpty(options[0].DialogText);
            Assert.AreEqual("挑战", options[1].Label);
            Assert.IsTrue(options[1].TriggerCombat);
            Assert.IsFalse(options[1].ShowChangeJob);
        }

        [Test]
        public void InteractConfig_Unconfigured_ReturnsNull_WithDefaultDialog()
        {
            Assert.IsNull(NpcManager.GetInteractOptions(NpcType.None));
            Assert.AreEqual("你好，旅行者！", NpcManager.GetDefaultDialog(NpcType.None));
        }

        [Test]
        public void IsInFootprint_Static_Range()
        {
            // 锚 (5,5)，2×2：覆盖 (5,5)(6,5)(5,6)(6,6)
            Assert.IsTrue(NpcManager.IsInFootprint(new Vector2Int(5, 5), 5, 5, 2, 2));
            Assert.IsTrue(NpcManager.IsInFootprint(new Vector2Int(6, 6), 5, 5, 2, 2));
            Assert.IsFalse(NpcManager.IsInFootprint(new Vector2Int(7, 5), 5, 5, 2, 2));
            Assert.IsFalse(NpcManager.IsInFootprint(new Vector2Int(5, 7), 5, 5, 2, 2));
            Assert.IsFalse(NpcManager.IsInFootprint(new Vector2Int(4, 5), 5, 5, 2, 2));
            // size 0 按 1 处理
            Assert.IsTrue(NpcManager.IsInFootprint(new Vector2Int(3, 3), 3, 3, 0, 0));
            Assert.IsFalse(NpcManager.IsInFootprint(new Vector2Int(4, 3), 3, 3, 0, 0));
        }

        [Test]
        public void GetAdjacentNpc_FourDirs_SingleCell()
        {
            var mgr = CreateMgrWithNpc(1, 5, 5);
            // 四邻格命中（上/下/左/右）
            Assert.NotNull(mgr.GetAdjacentNpc(new Vector2Int(5, 4)));
            Assert.NotNull(mgr.GetAdjacentNpc(new Vector2Int(5, 6)));
            Assert.NotNull(mgr.GetAdjacentNpc(new Vector2Int(4, 5)));
            Assert.NotNull(mgr.GetAdjacentNpc(new Vector2Int(6, 5)));
            // 斜角不算、远处不算
            Assert.IsNull(mgr.GetAdjacentNpc(new Vector2Int(4, 4)));
            Assert.IsNull(mgr.GetAdjacentNpc(new Vector2Int(10, 10)));
            Object.DestroyImmediate(mgr.gameObject);
        }

        [Test]
        public void GetAdjacentNpc_MultiCellFootprint()
        {
            // 2×1 NPC：中心 (5,5) → 锚 (5,5)，覆盖 (5,5)(6,5)
            var mgr = CreateMgrWithNpc(1, 5, 5, sizeX: 2, sizeY: 1);
            Assert.NotNull(mgr.GetAdjacentNpc(new Vector2Int(6, 4)), "第二格上邻应命中");
            Assert.NotNull(mgr.GetAdjacentNpc(new Vector2Int(7, 5)), "第二格右邻应命中");
            Assert.IsNull(mgr.GetAdjacentNpc(new Vector2Int(8, 5)), "footprint 外不应命中");
            Object.DestroyImmediate(mgr.gameObject);
        }

        [Test]
        public void IsBlockedByNpc_HitAndMiss()
        {
            var mgr = CreateMgrWithNpc(1, 5, 5, sizeX: 2, sizeY: 1);
            Assert.IsTrue(mgr.IsBlockedByNpc(new Vector2Int(5, 5)));
            Assert.IsTrue(mgr.IsBlockedByNpc(new Vector2Int(6, 5)));
            Assert.IsFalse(mgr.IsBlockedByNpc(new Vector2Int(7, 5)));
            Assert.IsFalse(mgr.IsBlockedByNpc(new Vector2Int(5, 4)));
            Object.DestroyImmediate(mgr.gameObject);
        }

        [Test]
        public void TriggerNpcCombat_OptimisticBarsShown_OfflineNoThrow()
        {
            var mgr = CreateMgrWithNpc(1, 5, 5, type: 2);
            var npc = mgr.GetNpcs().First();
            // NPC 条默认隐藏（Profile），挑战时乐观亮出
            Assert.IsFalse(npc.transform.Find("HpBar").gameObject.activeSelf);
            mgr.TriggerNpcCombat(npc); // 离线：不发包但不抛异常
            Assert.IsTrue(npc.transform.Find("HpBar").gameObject.activeSelf);
            Assert.IsTrue(npc.transform.Find("MpBar").gameObject.activeSelf);
            Object.DestroyImmediate(mgr.gameObject);
        }
    }
}
