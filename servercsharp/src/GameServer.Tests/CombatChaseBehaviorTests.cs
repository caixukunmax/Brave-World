using System.Collections.Concurrent;
using GameServer.Common.Config;
using GameServer.Services.Core;
using GameServer.Services.Monster;
using GameServer.Services.Monster.AI;
using Xunit;

namespace GameServer.Tests;

public class CombatChaseBehaviorTests
{
    [Fact]
    public void Run_WhenTargetAdjacent_HoldsPosition()
    {
        var behavior = new CombatChaseBehavior(CreateOpenMap());
        var monster = new MonsterRuntimeState
        {
            X = 2,
            Y = 2,
            TargetId = 1001,
            AiConfig = new AiConfig { ChaseIntervalMs = 0 },
        };
        var players = new Dictionary<long, PlayerStateView>
        {
            [1001] = new() { AccountId = 1001, GridX = 2, GridY = 3 },
        };

        var next = behavior.Run(monster, "test_map", players, new FakeWorldState());

        Assert.Null(next);
        Assert.Equal(MonsterState.CombatHold, monster.State);
        Assert.Equal(1001, monster.TargetId);
    }

    [Fact]
    public void Run_WhenTargetFar_ChasesOneStep()
    {
        var behavior = new CombatChaseBehavior(CreateOpenMap());
        var monster = new MonsterRuntimeState
        {
            X = 1,
            Y = 1,
            TargetId = 2002,
            AiConfig = new AiConfig { ChaseIntervalMs = 0 },
        };
        var players = new Dictionary<long, PlayerStateView>
        {
            [2002] = new() { AccountId = 2002, GridX = 4, GridY = 1 },
        };

        var next = behavior.Run(monster, "test_map", players, new FakeWorldState());

        Assert.Equal((2, 1), next);
        Assert.Equal(MonsterState.CombatChase, monster.State);
        Assert.Equal(2002, monster.TargetId);
    }

    [Fact]
    public void RangedBehavior_WhenWithinCastRange_HoldsPosition()
    {
        var behavior = new RangedCombatBehavior(CreateOpenMap());
        var monster = new MonsterRuntimeState
        {
            X = 1,
            Y = 1,
            TargetId = 3003,
            AiConfig = new AiConfig { ChaseIntervalMs = 0, CombatRange = 3 },
        };
        var players = new Dictionary<long, PlayerStateView>
        {
            [3003] = new() { AccountId = 3003, GridX = 4, GridY = 1 },
        };

        var next = behavior.Run(monster, "test_map", players, new FakeWorldState());

        Assert.Null(next);
        Assert.Equal(MonsterState.CombatRangedHold, monster.State);
    }

    private static MapDataProvider CreateOpenMap()
    {
        var provider = new MapDataProvider();
        // 5x5 全普通地形（可行走），JSON cell 字符串
        var cells = Enumerable.Repeat("{\"terrain\":0,\"height\":0,\"custom\":\"\"}", 25).ToArray();
        provider.LoadMap("test_map", 0, 0, 5, 5, cells);
        return provider;
    }

    private sealed class FakeWorldState : IWorldState
    {
        public (string? mapName, (int x, int y)? pos) FindEntityPosition(long entityId) => (null, null);
        public string GetEntityName(long entityId) => entityId.ToString();
        public ConcurrentDictionary<long, MapPlayerState> GetPlayersOnMap(string mapName) => new();
        public ConcurrentDictionary<long, MapMonsterState> GetMonstersOnMap(string mapName) => new();
        public bool IsWalkable(string mapName, int x, int y) => true;
        public (int x, int y)? FindNearestWalkable(string mapName, int x, int y) => (x, y);
        public (int x, int y)? FindNearestWalkableForFootprint(string mapName, int x, int y, int sizeX, int sizeY,
            int maxRadius = 10, bool requireVacant = true, long excludedEntityId = 0) => (x, y);
        public bool IsOccupied(string mapName, int x, int y) => false;
        public ConcurrentDictionary<string, MapState> GetAllMaps() => new();
    }
}
