using System.IO;
using NUnit.Framework;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace BraveWorld.Tests
{
    /// <summary>
    /// 地图编辑器 Runtime 侧测试：MapDataManager 地图管理（新建/列表）、
    /// GridManager.SaveCurrentMap（displayName/spawn 保留）、GridManager.ExtendMap。
    /// 均用 __unittest__ 地图名，SetUp/TearDown 双向清理。
    /// </summary>
    public class MapEditingEditModeTests
    {
        private const string TestMap = "__unittest__";
        private static string TestMapDir => Path.Combine(MapDataManager.MapsRoot, TestMap);

        [SetUp]
        public void SetUp()
        {
            // EditMode 下运行时 MonoBehaviour 的 Awake 不执行（AGENTS.md），静态配置显式加载
            TerrainConfigUtil.Load();
            Cleanup();
        }

        [TearDown]
        public void TearDown() => Cleanup();

        private static void Cleanup()
        {
            if (Directory.Exists(TestMapDir))
                Directory.Delete(TestMapDir, true);
        }

        [Test]
        public void CreateNewMap_AppearsInMapList()
        {
            Assert.IsTrue(MapDataManager.CreateNewMap(TestMap, 20, 15));
            Assert.Contains(TestMap, MapDataManager.GetMapList());
            // 同名重复创建应失败（LogError 是预期日志，显式吞掉）
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*地图已存在.*"));
            Assert.IsFalse(MapDataManager.CreateNewMap(TestMap, 20, 15));
        }

        [Test]
        public void SaveCurrentMap_PreservesSpawnAndDisplayName()
        {
            Assert.IsTrue(MapDataManager.CreateNewMap(TestMap, 20, 15));
            // 写入非默认 spawn/displayName（默认是图名与中心点）
            var data = MapDataManager.LoadMapFromJson(TestMap, out var bounds, out _, out _);
            Assert.IsTrue(MapDataManager.SaveMapToJson(TestMap, data, "单元测试图", bounds, new Vector2Int(3, 4)));

            var go = new GameObject("TestGrid");
            try
            {
                var gm = go.AddComponent<GridManager>();
                gm.LoadMap(TestMap);
                Assert.IsTrue(gm.SaveCurrentMap());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            MapDataManager.LoadMapFromJson(TestMap, out _, out var spawn, out var displayName);
            Assert.AreEqual(new Vector2Int(3, 4), spawn, "spawn 必须原样保留（LoadMap 丢弃即回归）");
            Assert.AreEqual("单元测试图", displayName, "display_name 必须原样保留");
        }

        [Test]
        public void ExtendMap_AddsCellsAndRecalculatesBounds()
        {
            Assert.IsTrue(MapDataManager.CreateNewMap(TestMap, 10, 10));
            var go = new GameObject("TestGrid");
            try
            {
                var gm = go.AddComponent<GridManager>();
                gm.LoadMap(TestMap);
                int before = gm.GridData.Count;

                int added = gm.ExtendMap(new[] { new Vector2Int(15, 15), new Vector2Int(16, 15) });
                Assert.AreEqual(2, added);
                Assert.AreEqual(before + 2, gm.GridData.Count);
                Assert.IsTrue(gm.IsInBounds(new Vector2Int(15, 15)));
                Assert.GreaterOrEqual(gm.MapBounds.xMax, 17, "bounds 必须覆盖新格");
                // 旧格不受影响
                Assert.AreEqual(0, gm.GetCell(Vector2Int.zero).TerrainType);
                // 重复扩展不新增
                Assert.AreEqual(0, gm.ExtendMap(new[] { new Vector2Int(15, 15) }));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
