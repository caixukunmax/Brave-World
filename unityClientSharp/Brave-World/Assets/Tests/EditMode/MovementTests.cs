using System.Collections.Generic;
using NUnit.Framework;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace BraveWorld.Tests
{
    /// <summary>移动同步阶段测试：缓动/方向/行走判定链/回弹参数。</summary>
    public class MovementTests
    {
        [Test]
        public void Ease_Endpoints_Exact()
        {
            foreach (SimpleTween.Ease ease in System.Enum.GetValues(typeof(SimpleTween.Ease)))
            {
                Assert.AreEqual(0f, SimpleTween.Evaluate(ease, 0f), 1e-6f, ease.ToString());
                Assert.AreEqual(1f, SimpleTween.Evaluate(ease, 1f), 1e-6f, ease.ToString());
            }
        }

        [Test]
        public void Ease_KnownMidpoints()
        {
            Assert.AreEqual(0.5f, SimpleTween.Evaluate(SimpleTween.Ease.Linear, 0.5f), 1e-6f);
            Assert.AreEqual(0.75f, SimpleTween.Evaluate(SimpleTween.Ease.QuadOut, 0.5f), 1e-6f);
            Assert.AreEqual(0.25f, SimpleTween.Evaluate(SimpleTween.Ease.QuadIn, 0.5f), 1e-6f);
            Assert.AreEqual(0.125f, SimpleTween.Evaluate(SimpleTween.Ease.CubicIn, 0.5f), 1e-6f);
            Assert.AreEqual(Mathf.Sin(0.5f * Mathf.PI / 2f), SimpleTween.Evaluate(SimpleTween.Ease.SineOut, 0.5f), 1e-6f);
        }

        [Test]
        public void DirectionFromVector_Mapping()
        {
            Assert.AreEqual(0, EntityVisualBase.DirectionFromVector(new Vector2Int(1, 0)));
            Assert.AreEqual(2, EntityVisualBase.DirectionFromVector(new Vector2Int(-1, 0)));
            Assert.AreEqual(1, EntityVisualBase.DirectionFromVector(new Vector2Int(0, 1)));
            Assert.AreEqual(3, EntityVisualBase.DirectionFromVector(new Vector2Int(0, -1)));
            Assert.AreEqual(1, EntityVisualBase.DirectionFromVector(Vector2Int.zero));
        }

        [Test]
        public void BounceBack_Constants_MatchGodot()
        {
            Assert.AreEqual(0.06f, EntityVisualBase.BounceBackDuration, 1e-6f);
            Assert.AreEqual(0.08f, EntityVisualBase.BounceBackOvershootRatio, 1e-6f);
            Assert.AreEqual(0.40f, EntityVisualBase.BounceBackOvershootThreshold, 1e-6f);
        }

        private GameObject _gridGo, _chestGo, _monsterGo;
        private GridManager _gm;
        private ChestManager _chestMgr;
        private MonsterManager _monsterMgr;

        private static GridCell Cell(int x, int y, int terrain)
        {
            var c = new GridCell(x, y) { TerrainType = terrain };
            c.RefreshTerrainConfig();
            return c;
        }

        [SetUp]
        public void SetUp()
        {
            TerrainConfigUtil.Load();

            _gridGo = new GameObject("Grid");
            _gm = _gridGo.AddComponent<GridManager>();
            _gm.GridData = new Dictionary<Vector2Int, GridCell>
            {
                [new Vector2Int(0, 0)] = Cell(0, 0, 0),
                [new Vector2Int(1, 0)] = Cell(1, 0, 0),
                [new Vector2Int(2, 0)] = Cell(2, 0, 0),
                [new Vector2Int(3, 0)] = Cell(3, 0, 0),
            };

            _chestGo = new GameObject("Chests");
            _chestMgr = _chestGo.AddComponent<ChestManager>();
            _chestMgr.GridSize = 111;

            _monsterGo = new GameObject("Monsters");
            _monsterMgr = _monsterGo.AddComponent<MonsterManager>();
            _monsterMgr.GridSize = 111;

            // EditMode 下 Awake 不执行，手动注册门面
            Walkability.Grid = _gm;
            Walkability.Chests = _chestMgr;
            Walkability.Monsters = _monsterMgr;
            Walkability.Decorations = null;
        }

        [TearDown]
        public void TearDown()
        {
            Walkability.Grid = null;
            Walkability.Chests = null;
            Walkability.Monsters = null;
            Object.DestroyImmediate(_monsterGo);
            Object.DestroyImmediate(_chestGo);
            Object.DestroyImmediate(_gridGo);
        }

        [Test]
        public void Walkability_UnopenedChest_Blocks()
        {
            _chestMgr.SpawnChests(new List<Game.ChestInfo>
            {
                new Game.ChestInfo { ChestId = 1, X = 1, Y = 0, Opened = false },
            });

            Assert.IsFalse(Walkability.IsWalkable(new Vector2Int(1, 0)));
            Assert.IsTrue(Walkability.IsWalkable(new Vector2Int(0, 0)));
            Assert.AreEqual(1u, _chestMgr.GetChestIdAt(new Vector2Int(1, 0)));
            Assert.AreEqual(0u, _chestMgr.GetChestIdAt(new Vector2Int(0, 0)));
        }

        [Test]
        public void Walkability_MonsterFootprint_Blocks()
        {
            _monsterMgr.SpawnMonsters(new List<Game.MonsterInfo>
            {
                new Game.MonsterInfo { InstanceId = 1, MonsterId = 1001, Name = "m", Level = 1, X = 2, Y = 0, SizeX = 1, SizeY = 1 },
            });

            Assert.IsFalse(Walkability.IsWalkable(new Vector2Int(2, 0)));
            Assert.IsTrue(Walkability.IsWalkable(new Vector2Int(1, 0)));
        }

        [Test]
        public void Walkability_MultiCellMonster_AnchorFromCenter()
        {
            // 2x2 怪物中心在 (2,0)：footprint 锚点 = center - (size-1)/2 = (2,0)（整数除法），覆盖 (2,0)(3,0)(2,1)(3,1)
            _monsterMgr.SpawnMonsters(new List<Game.MonsterInfo>
            {
                new Game.MonsterInfo { InstanceId = 1, MonsterId = 1001, Name = "m", Level = 1, X = 2, Y = 0, SizeX = 2, SizeY = 2 },
            });

            Assert.IsFalse(Walkability.IsWalkable(new Vector2Int(2, 0)));
            Assert.IsFalse(Walkability.IsWalkable(new Vector2Int(3, 0)));
            Assert.IsTrue(Walkability.IsWalkable(new Vector2Int(1, 0)));
        }
    }
}
