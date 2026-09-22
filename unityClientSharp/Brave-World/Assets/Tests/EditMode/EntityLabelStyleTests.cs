using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;
using UnityEngine;

namespace BraveWorld.Tests
{
    /// <summary>
    /// 标签字体配置三端一致性测试（调试面板预览 / 地图编辑器 / 游戏运行时）。
    /// 回归目标：粗体/斜体/阴影/行内字号与 labels「停用」在运行时与调试面板预览语义一致。
    /// </summary>
    public class EntityLabelStyleTests
    {
        private const int TestProfileId = 990001;

        [TearDown]
        public void TearDown()
        {
            EntityProfileManager.RemoveProfile(TestProfileId);
        }

        [Test]
        public void RowFontSize_RowOverrideAndGlobalDefault()
        {
            var labels = new LabelGroupData { DefaultFontSize = 20 };
            // 行内未指定（0）且用默认 → 全局默认 20
            Assert.AreEqual(20, EntityAppearanceLayout.RowFontSize(111, labels, 0, 1f, 3f / 111f));
            // 行内指定且不用默认 → 行内字号
            labels.UseGlobalFontSize[1] = false;
            labels.FontSizes[1] = 33;
            Assert.AreEqual(33, EntityAppearanceLayout.RowFontSize(111, labels, 1, 1f, 3f / 111f));
            // 行内指定但勾了「使用默认」→ 仍走全局默认
            labels.UseGlobalFontSize[2] = true;
            labels.FontSizes[2] = 40;
            Assert.AreEqual(20, EntityAppearanceLayout.RowFontSize(111, labels, 2, 1f, 3f / 111f));
            // 全局 0=自动 → 与 ComputeFontSize 自动值一致
            labels.DefaultFontSize = 0;
            Assert.AreEqual(
                EntityAppearanceLayout.ComputeFontSize(111, 0, 1f, 3f / 111f),
                EntityAppearanceLayout.RowFontSize(111, labels, 0, 1f, 3f / 111f));
        }

        [Test]
        public void RowTextColor_GlobalVsRowOverride()
        {
            var labels = new LabelGroupData { DefaultTextColor = Color.yellow };
            labels.TextColors[0] = Color.red;
            Assert.AreEqual(Color.yellow, EntityAppearanceLayout.RowTextColor(labels, 0));
            labels.UseGlobalFontSize[0] = false;
            Assert.AreEqual(Color.red, EntityAppearanceLayout.RowTextColor(labels, 0));
        }

        [Test]
        public void MapDecoration_LabelStyle_BoldItalicShadowApplied()
        {
            var p = EntityProfile.CreateDecorationDefault(TestProfileId, "t", "测试屋",
                BuildingType.House, Color.white, Color.white, false, 1, 1, "");
            var labels = p.GetData<LabelGroupData>("labels");
            labels.Bold = true;
            labels.Italic = true;
            labels.Shadow = true;
            EntityProfileManager.AddProfile(p);

            var go = new GameObject("dec");
            try
            {
                var dec = go.AddComponent<MapDecoration>();
                dec.Setup(TestProfileId, 0, 0, 111);

                var labelGo = go.transform.Find("Label");
                Assert.NotNull(labelGo, "应生成主标签");
                var label = labelGo.GetComponent<TextMeshPro>();
                Assert.IsTrue((label.fontStyle & FontStyles.Bold) != 0, "粗体未应用");
                Assert.IsTrue((label.fontStyle & FontStyles.Italic) != 0, "斜体未应用");
                // 世界空间字号约定：isOrthographic=true，fontSize 即世界单位（FontUtil.SetWorldFontSize）
                Assert.IsTrue(label.isOrthographic, "世界空间标签必须 isOrthographic=true，否则 TMP 内建 0.1 缩放使字小 10 倍");
                Assert.IsFalse(label.enableWordWrapping,
                    "世界空间标签必须关闭换行（运行时 TMP RectTransform 宽 0，默认换行会逐字折行，与调试面板预览单行不一致）");
                int expectFs = EntityAppearanceLayout.RowFontSize(111, labels, 0, 1f, 3f / 111f);
                Assert.AreEqual(expectFs, label.fontSize, 1e-3f);

                var shadowGo = go.transform.Find("LabelShadow");
                Assert.NotNull(shadowGo, "开启阴影应生成阴影副本");
                var shadow = shadowGo.GetComponent<TextMeshPro>();
                Assert.AreEqual(label.text, shadow.text);
                Assert.AreEqual(label.fontSize, shadow.fontSize);
                Assert.IsTrue(shadow.isOrthographic, "阴影副本必须跟随主标签的字号约定");
                Assert.IsFalse(shadow.enableWordWrapping, "阴影副本必须跟随主标签的换行设置，否则阴影与主标签错位");
                Assert.Less(shadowGo.GetComponent<MeshRenderer>().sortingOrder,
                    labelGo.GetComponent<MeshRenderer>().sortingOrder, "阴影应排在主标签之下");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MapDecoration_LabelsDisabled_NoLabelCreated()
        {
            var p = EntityProfile.CreateDecorationDefault(TestProfileId, "t", "测试屋",
                BuildingType.House, Color.white, Color.white, false, 1, 1, "");
            p.SetComponentDisabled("labels", true);
            EntityProfileManager.AddProfile(p);

            var go = new GameObject("dec");
            try
            {
                var dec = go.AddComponent<MapDecoration>();
                dec.Setup(TestProfileId, 0, 0, 111);
                Assert.IsNull(go.transform.Find("Label"), "labels 组件停用时不应生成标签（与调试面板预览一致）");
                Assert.IsNull(go.transform.Find("LabelShadow"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EntityVisualBase_SetLabel_SyncsShadowText()
        {
            // MonsterEntity 固定用 Profile 2，临时开阴影（用例结束还原，避免污染其它用例）
            var labels = EntityProfileManager.GetProfile(2).GetData<LabelGroupData>("labels");
            bool oldShadow = labels.Shadow;
            labels.Shadow = true;

            var go = new GameObject("MonsterMgr");
            try
            {
                var mgr = go.AddComponent<MonsterManager>();
                mgr.GridSize = 111;
                mgr.SpawnMonsters(new List<Game.MonsterInfo>
                {
                    new Game.MonsterInfo { InstanceId = 7, MonsterId = 1001, Name = "史莱姆", Level = 3, X = 10, Y = 20, SizeX = 1, SizeY = 1, Direction = 1 },
                });
                var m = mgr.GetComponentInChildren<MonsterEntity>();
                Assert.NotNull(m);

                var shadowGo = m.transform.Find("Label0Shadow");
                Assert.NotNull(shadowGo, "开启阴影应生成阴影副本");

                m.SetLabel(0, "新名字");
                Assert.AreEqual("新名字", shadowGo.GetComponent<TextMeshPro>().text,
                    "SetLabel 必须同步阴影副本文本");
            }
            finally
            {
                labels.Shadow = oldShadow;
                Object.DestroyImmediate(go);
            }
        }
    }
}
