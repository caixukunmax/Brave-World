using System.Collections.Generic;
using NUnit.Framework;
using UnityClientSharp.Entity;
using UnityEngine;

namespace BraveWorld.Tests
{
    /// <summary>在线实体显示层测试。</summary>
    public class OnlineEntityTests
    {
        [Test]
        public void DefaultProfiles_PlayerMonsterNpc_Registered()
        {
            var player = EntityProfileManager.GetProfile(1);
            Assert.NotNull(player);
            Assert.AreEqual(0.35f, player.GetData<AppearanceData>("appearance").BgOpacity, 1e-4f);

            var monster = EntityProfileManager.GetProfile(2);
            Assert.NotNull(monster);
            Assert.AreEqual(new Color(0.8f, 0.2f, 0.2f), monster.GetData<AppearanceData>("appearance").BgColor);
            Assert.IsFalse(monster.GetData<LabelGroupData>("labels").Visible[2]); // 预留行隐藏

            var npc = EntityProfileManager.GetProfile(3);
            Assert.NotNull(npc);
            Assert.IsFalse(npc.GetData<BarData>("healthbar").Visible); // NPC 条隐藏
        }

        [Test]
        public void BarData_Defaults_MatchGodot()
        {
            var hp = BarData.CreateHealthBarDefault();
            Assert.AreEqual(-70f, hp.OffsetY);
            Assert.AreEqual(102f / 111f, hp.LengthScale, 1e-4f);
            var mp = BarData.CreateMpBarDefault();
            Assert.AreEqual(-62f, mp.OffsetY);
            Assert.AreEqual(80f / 111f, mp.LengthScale, 1e-4f);
        }

        [Test]
        public void MonsterStateText_Mapping()
        {
            Assert.AreEqual("待机", MonsterEntity.StateToText("idle"));
            Assert.AreEqual("巡逻", MonsterEntity.StateToText("patrol"));
            Assert.AreEqual("追杀", MonsterEntity.StateToText("combat_chase"));
            Assert.AreEqual("死亡", MonsterEntity.StateToText("dead"));
            Assert.AreEqual("待机", MonsterEntity.StateToText("unknown_state"));
        }

        [Test]
        public void QualityColor_Table()
        {
            Assert.AreEqual(Color.white, ItemIconCatalog.GetQualityColor(0));
            Assert.AreEqual(new Color(0.1f, 1f, 0.1f), ItemIconCatalog.GetQualityColor(1));
            Assert.AreEqual(new Color(0.1f, 0.5f, 1f), ItemIconCatalog.GetQualityColor(2));
            Assert.AreEqual(new Color(0.6f, 0.2f, 1f), ItemIconCatalog.GetQualityColor(3));
            Assert.AreEqual(new Color(1f, 0.5f, 0f), ItemIconCatalog.GetQualityColor(4));
        }

        [Test]
        public void ChestManager_OpenedChest_NotSpawned()
        {
            var go = new GameObject("ChestMgr");
            var mgr = go.AddComponent<ChestManager>();
            mgr.GridSize = 111;

            mgr.SpawnChests(new List<Game.ChestInfo>
            {
                new Game.ChestInfo { ChestId = 1, X = 3, Y = 3, Opened = true },
                new Game.ChestInfo { ChestId = 2, X = 5, Y = 5, Opened = false },
            });

            // 只有未开的生成
            Assert.NotNull(mgr.GetComponentInChildren<ChestEntity>());
            var chests = mgr.GetComponentsInChildren<ChestEntity>();
            Assert.AreEqual(1, chests.Length);
            Assert.AreEqual(2u, chests[0].ChestId);
            Assert.AreEqual(new Vector2Int(5, 5), chests[0].GridPos);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MonsterSpawn_LabelsAndPosition()
        {
            var go = new GameObject("MonsterMgr");
            var mgr = go.AddComponent<MonsterManager>();
            mgr.GridSize = 111;

            mgr.SpawnMonsters(new List<Game.MonsterInfo>
            {
                new Game.MonsterInfo { InstanceId = 7, MonsterId = 1001, Name = "史莱姆", Level = 3, X = 10, Y = 20, SizeX = 1, SizeY = 1, Direction = 1 },
            });

            var monsters = mgr.GetComponentsInChildren<MonsterEntity>();
            Assert.AreEqual(1, monsters.Length);
            var m = monsters[0];
            Assert.AreEqual(7u, m.InstanceId);
            Assert.AreEqual(new Vector2Int(10, 20), m.GridPos);

            // 世界位置 = 格子中心 y 取负：(10*111+55.5, -(20*111+55.5))
            Assert.AreEqual(10 * 111 + 55.5f, m.transform.localPosition.x, 0.01f);
            Assert.AreEqual(-(20 * 111 + 55.5f), m.transform.localPosition.y, 0.01f);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PlayerApplyRoleInfo_LabelsAndBars()
        {
            var roleInfo = new Game.FullRoleInfo
            {
                RoleName = "测试勇者",
                Level = 5,
                Job = "农夫",
                Title = "普通人",
                GridX = 40,
                GridY = 64,
            };
            roleInfo.Attrs.Add(new Game.AttrItem { Key = 1, Value = 80 });  // hp
            roleInfo.Attrs.Add(new Game.AttrItem { Key = 2, Value = 100 }); // maxHp
            roleInfo.Attrs.Add(new Game.AttrItem { Key = 3, Value = 30 });  // mp
            roleInfo.Attrs.Add(new Game.AttrItem { Key = 4, Value = 50 });  // maxMp

            var go = new GameObject("Player");
            var player = go.AddComponent<PlayerEntity>();
            player.SetupFromRoleInfo(roleInfo, 111);

            Assert.AreEqual("测试勇者", player.CharacterName);
            Assert.AreEqual(5, player.Level);
            Assert.AreEqual(new Vector2Int(40, 64), player.GridPos);
            Assert.AreEqual(40 * 111 + 55.5f, player.transform.localPosition.x, 0.01f);

            Object.DestroyImmediate(go);
        }
    }
}
