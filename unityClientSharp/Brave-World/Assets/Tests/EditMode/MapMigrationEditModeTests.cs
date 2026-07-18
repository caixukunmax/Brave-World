using System.Collections.Generic;
using NUnit.Framework;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace BraveWorld.Tests
{
    /// <summary>GridMath 坐标换算（含 Y 翻转单点约定与负坐标）。</summary>
    public class GridMathTests
    {
        [Test]
        public void GridToWorld_CellCenter_AlignedToInt()
        {
            Assert.AreEqual(new Vector2(56, 56), GridMath.GridToWorld(0, 0, 111));
            Assert.AreEqual(new Vector2(166, 56), GridMath.GridToWorld(1, 0, 111));
        }

        [Test]
        public void WorldToGrid_NegativeCoord_FloorsDown()
        {
            Assert.AreEqual(new Vector2Int(-1, -1), GridMath.WorldToGrid(new Vector2(-0.5f, -1f), 111));
            Assert.AreEqual(new Vector2Int(0, 0), GridMath.WorldToGrid(new Vector2(110.9f, 0f), 111));
        }

        [Test]
        public void LogicToWorld_FlipsY()
        {
            Assert.AreEqual(new Vector2(10f, -20f), GridMath.LogicToWorld(10f, 20f));
        }

        [Test]
        public void FootprintCenterWorld_2x2_IsAnchorPlusOneCell()
        {
            // 2x2 建筑锚点 (5,5)：逻辑中心 = (5*111+111, 5*111+111) = (666, 666)，世界 y 取负
            var w = GridMath.FootprintCenterWorld(5, 5, 2, 2, 111);
            Assert.AreEqual(666f, w.x, 1e-4f);
            Assert.AreEqual(-666f, w.y, 1e-4f);
            Assert.AreEqual(0f, w.z);
        }
    }

    /// <summary>BuildingType 配置/UID 编解码。</summary>
    public class BuildingTypeTests
    {
        [Test]
        public void ConfigAndUid_RoundTrip()
        {
            Assert.AreEqual(110000, BuildingType.GetConfigBaseId(BuildingType.Grass));
            Assert.AreEqual(BuildingType.Grass, BuildingType.GetTypeFromConfigId(110000));
            Assert.AreEqual(600000, BuildingType.GetUidBaseId(BuildingType.SpawnPoint));
            Assert.AreEqual(BuildingType.SpawnPoint, BuildingType.GetTypeFromUid(600000));
        }
    }

    /// <summary>装饰 Profile 默认注册。</summary>
    public class EntityProfileManagerTests
    {
        [Test]
        public void House10001_Is2x2BlockingBuilding()
        {
            var p = EntityProfileManager.GetProfile(10001);
            Assert.NotNull(p);
            Assert.AreEqual("decoration", p.EntityType);
            var app = p.GetData<AppearanceData>("appearance");
            Assert.AreEqual(2, app.SizeX);
            Assert.AreEqual(2, app.SizeY);
            Assert.IsTrue(p.GetData<ObstacleData>("obstacle").BlockMovement);
            Assert.AreEqual("房舍", p.GetData<LabelGroupData>("labels").ContentPreview[0]);
        }

        [Test]
        public void Grass110000_IsNonBlockingTerrain()
        {
            var p = EntityProfileManager.GetProfile(110000);
            Assert.NotNull(p);
            Assert.IsFalse(p.GetData<ObstacleData>("obstacle").BlockMovement);
            Assert.AreEqual("Terrain", p.GetData<CategoryData>("category").Category);
        }

        [Test]
        public void DecorationProfiles_Count14()
        {
            Assert.AreEqual(14, new List<EntityProfile>(EntityProfileManager.GetProfilesByType("decoration")).Count);
        }
    }

    /// <summary>装饰生成：多格占地去重、旧值兼容、出生点跳过、行走阻挡。</summary>
    public class MapDecorationManagerTests
    {
        private GameObject _root;
        private GridManager _gm;
        private MapDecorationManager _mgr;

        [SetUp]
        public void SetUp()
        {
            // EditMode 下运行时 MonoBehaviour 的 Awake 不会执行（无 ExecuteAlways），
            // 地形配置需显式加载（Load 幂等）
            TerrainConfigUtil.Load();
            _root = new GameObject("TestRoot");
            _gm = _root.AddComponent<GridManager>();
            var go = new GameObject("DecoMgr");
            _mgr = go.AddComponent<MapDecorationManager>();
            _mgr.GridSize = _gm.GridSize;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_mgr.gameObject);
            Object.DestroyImmediate(_root);
        }

        private static GridCell Cell(int x, int y, int terrain, int decoration)
        {
            var c = new GridCell(x, y) { TerrainType = terrain, DecorationType = decoration };
            c.RefreshTerrainConfig();
            return c;
        }

        [Test]
        public void SpawnDecorations_2x2House_DedupesFootprintCells()
        {
            // 真实地图中 footprint 每格都写 decoration：2x2 房舍 4 个格子都应被去重为 1 个实例
            var grid = new Dictionary<Vector2Int, GridCell>();
            for (int dx = 0; dx < 2; dx++)
                for (int dy = 0; dy < 2; dy++)
                    grid[new Vector2Int(5 + dx, 5 + dy)] = Cell(5 + dx, 5 + dy, 0, 10001);

            _mgr.SpawnDecorations(grid);

            var dec = _mgr.GetDecorationAt(new Vector2Int(6, 6));
            Assert.NotNull(dec);
            Assert.AreEqual(5, dec.GridX);
            Assert.AreEqual(5, dec.GridY);
            Assert.AreEqual(2, dec.SizeX);
            Assert.AreEqual(2, dec.SizeY);
            Assert.AreSame(dec, _mgr.GetDecorationAt(new Vector2Int(5, 5)));
        }

        [Test]
        public void SpawnDecoration_SpawnPoint_SkippedWhenNotEditable()
        {
            _mgr.SpawnEditable = false;
            var dec = _mgr.SpawnDecoration(new Vector2Int(1, 1), 60000);
            Assert.IsNull(dec);
        }

        [Test]
        public void SpawnDecoration_RawLegacyTypeIds_RejectedByGuard()
        {
            // Godot 同款顺序：先按 1-3 拒绝，因此原始旧值 1/2 永远到不了映射分支；
            // 旧地图的 1/10/11/12 在 JSON 加载期已由 GridCell.MigrateOldDecorationType 转换（见 MapDataManager）。
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("拒绝使用玩家/怪物/NPC Profile"));
            Assert.IsNull(_mgr.SpawnDecoration(new Vector2Int(2, 2), 1));
        }

        [Test]
        public void SpawnDecoration_PlayerProfileId_Rejected()
        {
            // 1-3 为玩家/怪物/NPC Profile，拒绝作为建筑装饰（与 Godot 顺序一致：先拒绝后映射）
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("拒绝使用玩家/怪物/NPC Profile"));
            Assert.IsNull(_mgr.SpawnDecoration(new Vector2Int(3, 3), 2));
        }

        [Test]
        public void IsWalkable_TerrainWallAndBuildingFootprint_Blocked()
        {
            var grid = new Dictionary<Vector2Int, GridCell>
            {
                [new Vector2Int(0, 0)] = Cell(0, 0, 0, 0),     // 普通
                [new Vector2Int(1, 0)] = Cell(1, 0, 9, 0),     // 地形墙
                [new Vector2Int(0, 1)] = Cell(0, 1, 0, 30000), // 水井（阻挡）
                [new Vector2Int(1, 1)] = Cell(1, 1, 0, 110000) // 草地（不阻挡）
            };
            _gm.GridData = grid;
            _mgr.SpawnDecorations(grid);

            Assert.IsTrue(_gm.IsWalkable(new Vector2Int(0, 0), _mgr));
            Assert.IsFalse(_gm.IsWalkable(new Vector2Int(1, 0), _mgr));
            Assert.IsFalse(_gm.IsWalkable(new Vector2Int(0, 1), _mgr));
            Assert.IsTrue(_gm.IsWalkable(new Vector2Int(1, 1), _mgr));
            Assert.IsFalse(_gm.IsWalkable(new Vector2Int(99, 99), _mgr)); // 不存在的格子
        }
    }
}
