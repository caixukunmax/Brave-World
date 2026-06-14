using GameServer.Common.Config;
using GameServer.Services.Core;
using GameServer.Services.World;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.Tests;

public class WorldStateSpatialIndexTests
{
    private static WorldState CreateWorldState()
    {
        var mapData = new MapDataProvider();
        // 5x5 地图，所有格子可行走（terrain=1）
        var cells = Enumerable.Range(0, 25).Select(_ => "{\"terrain\":1,\"height\":0}").ToArray();
        mapData.LoadMap("test_map", 0, 0, 5, 5, cells);
        return new WorldState(mapData, NullLogger<WorldState>.Instance);
    }

    [Fact]
    public void PlayerEnter_AddsToSpatialIndex()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState { AccountId = 1, GridX = 2, GridY = 2, RoleName = "A" });

        Assert.True(ws.IsOccupied("test_map", 2, 2));
        Assert.Equal(("test_map", (2, 2)), ws.FindEntityPosition(1));
        Assert.Equal("test_map", ws.GetEntityMapName(1));
    }

    [Fact]
    public void PlayerMove_UpdatesSpatialIndex()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState { AccountId = 1, GridX = 1, GridY = 1, RoleName = "A" });

        ws.PlayerMove(1, "test_map", 3, 3);

        Assert.False(ws.IsOccupied("test_map", 1, 1));
        Assert.True(ws.IsOccupied("test_map", 3, 3));
        Assert.Equal(("test_map", (3, 3)), ws.FindEntityPosition(1));
    }

    [Fact]
    public void PlayerLeave_RemovesFromSpatialIndex()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState { AccountId = 1, GridX = 1, GridY = 1, RoleName = "A" });

        ws.PlayerLeave(1, "test_map");

        Assert.False(ws.IsOccupied("test_map", 1, 1));
        Assert.Null(ws.FindEntityPosition(1).mapName);
        Assert.Null(ws.GetEntityMapName(1));
    }

    [Fact]
    public void MonsterEnterMoveLeave_UpdatesSpatialIndex()
    {
        var ws = CreateWorldState();
        ws.MonsterEnter("test_map", new MapMonsterState { InstanceId = 100, X = 1, Y = 1, Name = "Slime" });

        Assert.True(ws.IsOccupied("test_map", 1, 1));
        Assert.Equal(("test_map", (1, 1)), ws.FindEntityPosition(100));

        ws.MonsterMove(100, "test_map", 4, 4);
        Assert.False(ws.IsOccupied("test_map", 1, 1));
        Assert.True(ws.IsOccupied("test_map", 4, 4));
        Assert.Equal(("test_map", (4, 4)), ws.FindEntityPosition(100));

        ws.MonsterLeave(100, "test_map");
        Assert.False(ws.IsOccupied("test_map", 4, 4));
        Assert.Null(ws.FindEntityPosition(100).mapName);
    }

    [Fact]
    public void NpcEnter_AddsToSpatialIndex()
    {
        var ws = CreateWorldState();
        ws.NpcEnter("test_map", new MapNpcState { InstanceId = 200, X = 2, Y = 2, Name = "Guide" });

        Assert.True(ws.IsOccupied("test_map", 2, 2));
        Assert.Equal(("test_map", (2, 2)), ws.FindEntityPosition(200));
        Assert.Equal("test_map", ws.GetEntityMapName(200));
    }

    [Fact]
    public void HasEnemyAt_DetectsPlayersAndMonsters_ButNotNpcs()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState { AccountId = 1, GridX = 2, GridY = 2, RoleName = "A" });
        ws.NpcEnter("test_map", new MapNpcState { InstanceId = 200, X = 2, Y = 2, Name = "Guide" });

        Assert.True(ws.HasEnemyAt("test_map", 2, 2, excludeEntityId: 999));
        Assert.False(ws.HasEnemyAt("test_map", 2, 2, excludeEntityId: 1));

        ws.PlayerLeave(1, "test_map");
        Assert.False(ws.HasEnemyAt("test_map", 2, 2, excludeEntityId: 999));

        ws.MonsterEnter("test_map", new MapMonsterState { InstanceId = 100, X = 2, Y = 2, Name = "Slime" });
        Assert.True(ws.HasEnemyAt("test_map", 2, 2, excludeEntityId: 999));
        Assert.False(ws.HasEnemyAt("test_map", 2, 2, excludeEntityId: 100));
    }

    [Fact]
    public void TryReserveMove_BlockedByOtherEntitiesAndNpcs()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState { AccountId = 1, GridX = 1, GridY = 1, RoleName = "A" });
        ws.PlayerEnter("test_map", new MapPlayerState { AccountId = 2, GridX = 2, GridY = 2, RoleName = "B" });

        Assert.False(ws.TryReserveMove(1, "test_map", 1, 1, 2, 2, 300, 30, 40, 70));

        ws.NpcEnter("test_map", new MapNpcState { InstanceId = 200, X = 3, Y = 3, Name = "Guide" });
        Assert.False(ws.TryReserveMove(1, "test_map", 1, 1, 3, 3, 300, 30, 40, 70));

        Assert.True(ws.TryReserveMove(1, "test_map", 1, 1, 4, 4, 300, 30, 40, 70));
    }

    [Fact]
    public void MultipleEntities_OnSameCell_AreAllTracked()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState { AccountId = 1, GridX = 2, GridY = 2, RoleName = "A" });
        ws.MonsterEnter("test_map", new MapMonsterState { InstanceId = 100, X = 2, Y = 2, Name = "Slime" });
        ws.NpcEnter("test_map", new MapNpcState { InstanceId = 200, X = 2, Y = 2, Name = "Guide" });

        Assert.True(ws.IsOccupied("test_map", 2, 2));
        Assert.True(ws.HasEnemyAt("test_map", 2, 2, excludeEntityId: 999));

        ws.PlayerLeave(1, "test_map");
        Assert.True(ws.IsOccupied("test_map", 2, 2));
        Assert.True(ws.HasEnemyAt("test_map", 2, 2, excludeEntityId: 999));

        ws.MonsterLeave(100, "test_map");
        Assert.True(ws.IsOccupied("test_map", 2, 2));
        Assert.False(ws.HasEnemyAt("test_map", 2, 2, excludeEntityId: 999));

        ws.NpcEnter("test_map", new MapNpcState { InstanceId = 201, X = 2, Y = 2, Name = "Guide2" });
        Assert.True(ws.IsOccupied("test_map", 2, 2));
    }

    [Fact]
    public void GridIndex_MatchesAuthoritativeDictionaries_AfterMixedOperations()
    {
        var ws = CreateWorldState();
        var rand = new Random(42);
        const int count = 50;

        var players = new List<long>();
        var monsters = new List<long>();
        var npcs = new List<long>();

        for (int i = 0; i < count; i++)
        {
            long pid = i + 1;
            players.Add(pid);
            ws.PlayerEnter("test_map", new MapPlayerState
            {
                AccountId = pid,
                GridX = rand.Next(0, 5),
                GridY = rand.Next(0, 5),
                RoleName = $"P{i}"
            });
        }

        for (int i = 0; i < count; i++)
        {
            long mid = 1000 + i;
            monsters.Add(mid);
            ws.MonsterEnter("test_map", new MapMonsterState
            {
                InstanceId = mid,
                X = rand.Next(0, 5),
                Y = rand.Next(0, 5),
                Name = $"M{i}"
            });
        }

        for (int i = 0; i < count; i++)
        {
            long nid = 2000 + i;
            npcs.Add(nid);
            ws.NpcEnter("test_map", new MapNpcState
            {
                InstanceId = nid,
                X = rand.Next(0, 5),
                Y = rand.Next(0, 5),
                Name = $"N{i}"
            });
        }

        // 随机移动一半实体
        foreach (var pid in players.Take(count / 2))
            ws.PlayerMove(pid, "test_map", rand.Next(0, 5), rand.Next(0, 5));
        foreach (var mid in monsters.Take(count / 2))
            ws.MonsterMove(mid, "test_map", rand.Next(0, 5), rand.Next(0, 5));

        // 随机离开部分实体
        foreach (var pid in players.Take(count / 4))
            ws.PlayerLeave(pid, "test_map");
        foreach (var mid in monsters.Take(count / 4))
            ws.MonsterLeave(mid, "test_map");

        // 重建期望的格子→实体集合
        var map = ws.GetMapState("test_map")!;
        var expected = new Dictionary<(int x, int y), HashSet<long>>();
        void Add((int x, int y) pos, long id)
        {
            if (!expected.TryGetValue(pos, out var set))
            {
                set = new HashSet<long>();
                expected[pos] = set;
            }
            set.Add(id);
        }

        foreach (var p in map.Players.Values)
            Add((p.GridX, p.GridY), p.AccountId);
        foreach (var m in map.Monsters.Values)
            Add((m.X, m.Y), m.InstanceId);
        foreach (var n in map.Npcs.Values)
            Add((n.X, n.Y), n.InstanceId);

        // 校验 _entityLocations
        foreach (var p in map.Players.Values)
            Assert.Equal(("test_map", (p.GridX, p.GridY)), ws.FindEntityPosition(p.AccountId));
        foreach (var m in map.Monsters.Values)
            Assert.Equal(("test_map", (m.X, m.Y)), ws.FindEntityPosition(m.InstanceId));
        foreach (var n in map.Npcs.Values)
            Assert.Equal(("test_map", (n.X, n.Y)), ws.FindEntityPosition(n.InstanceId));

        // 校验 GridEntities 与权威字典一致
        Assert.Equal(expected.Count, map.GridEntities.Count);
        foreach (var (pos, expectedSet) in expected)
        {
            Assert.True(map.GridEntities.TryGetValue(pos, out var actualSet), $"Missing grid index at {pos}");
            Assert.Equal(expectedSet, actualSet);
        }

        // 反向校验：GridEntities 中没有权威字典不存在的实体
        foreach (var (pos, actualSet) in map.GridEntities)
        {
            var expectedAtPos = expected.GetValueOrDefault(pos) ?? new HashSet<long>();
            Assert.Equal(expectedAtPos, actualSet);
        }
    }
}
