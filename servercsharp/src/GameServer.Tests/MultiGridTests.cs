using GameServer.Common.Config;
using GameServer.Services.Core;
using GameServer.Services.World;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.Tests;

public class MultiGridTests
{
    private static string GetBuildingsJsonPath()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var path = Path.Combine(dir, "data", "buildings.json");
            if (File.Exists(path)) return path;
            dir = Directory.GetParent(dir)?.FullName;
            if (dir == null) break;
        }
        return "data/buildings.json";
    }

    private static MapDataProvider CreateMapDataWithBuildingConfig()
    {
        var buildings = new BuildingConfigProvider();
        buildings.Load(GetBuildingsJsonPath());
        return new MapDataProvider(null, buildings);
    }

    private static WorldState CreateWorldState(MapDataProvider? mapData = null)
    {
        if (mapData == null)
        {
            mapData = new MapDataProvider();
            var cells = Enumerable.Range(0, 25).Select(_ => "{\"terrain\":1,\"height\":0}").ToArray();
            mapData.LoadMap("test_map", 0, 0, 5, 5, cells);
        }
        return new WorldState(mapData, NullLogger<WorldState>.Instance);
    }

    [Fact]
    public void BuildingConfigProvider_LoadsHouseAs2x2Blocking()
    {
        var cfg = new BuildingConfigProvider();
        cfg.Load(GetBuildingsJsonPath());

        var (sx, sy) = cfg.GetSize(10000);
        Assert.Equal(2, sx);
        Assert.Equal(2, sy);
        Assert.True(cfg.BlocksMovement(10000));

        var (sx1, sy1) = cfg.GetSize(20001);
        Assert.Equal(1, sx1);
        Assert.Equal(1, sy1);
        Assert.False(cfg.BlocksMovement(20001));

        // 未知 id 默认 1x1 不阻塞
        var (sxU, syU) = cfg.GetSize(99999);
        Assert.Equal(1, sxU);
        Assert.Equal(1, syU);
        Assert.False(cfg.BlocksMovement(99999));
    }

    [Fact]
    public void MapDataProvider_ExpandsBuildingFootprint()
    {
        var mapData = CreateMapDataWithBuildingConfig();
        // 5x5 地图，(1,1) 放置 2x2 房舍 10000
        var cells = Enumerable.Range(0, 25).Select(_ => "{\"terrain\":1,\"height\":0}").ToArray();
        cells[1 * 5 + 1] = "{\"terrain\":1,\"height\":0,\"decoration\":10000}";
        mapData.LoadMap("test_map", 0, 0, 5, 5, cells);

        // footprint 内 4 格都不可行走
        Assert.False(mapData.IsWalkable("test_map", 1, 1));
        Assert.False(mapData.IsWalkable("test_map", 2, 1));
        Assert.False(mapData.IsWalkable("test_map", 1, 2));
        Assert.False(mapData.IsWalkable("test_map", 2, 2));

        // footprint 外仍可走
        Assert.True(mapData.IsWalkable("test_map", 0, 0));
        Assert.True(mapData.IsWalkable("test_map", 3, 3));
    }

    [Fact]
    public void MapDataProvider_UnknownDecoration_DoesNotBlock()
    {
        var mapData = CreateMapDataWithBuildingConfig();
        var cells = Enumerable.Range(0, 25).Select(_ => "{\"terrain\":1,\"height\":0}").ToArray();
        cells[1 * 5 + 1] = "{\"terrain\":1,\"height\":0,\"decoration\":99999}";
        mapData.LoadMap("test_map", 0, 0, 5, 5, cells);

        Assert.True(mapData.IsWalkable("test_map", 1, 1));
    }

    [Fact]
    public void WorldState_2x2Player_OccupiesFootprint()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 1,
            GridY = 1,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });

        Assert.True(ws.IsOccupied("test_map", 1, 1));
        Assert.True(ws.IsOccupied("test_map", 2, 1));
        Assert.True(ws.IsOccupied("test_map", 1, 2));
        Assert.True(ws.IsOccupied("test_map", 2, 2));
        Assert.False(ws.IsOccupied("test_map", 3, 3));

        var footprint = ws.GetEntityFootprint(1);
        Assert.Equal(4, footprint.Count);
        Assert.Contains((1, 1), footprint);
        Assert.Contains((2, 2), footprint);
    }

    [Fact]
    public void WorldState_2x2PlayerMove_UpdatesFootprint()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 1,
            GridY = 1,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });

        ws.PlayerMove(1, "test_map", 2, 1);

        // 旧 footprint 清空
        Assert.False(ws.IsOccupied("test_map", 1, 1));
        Assert.False(ws.IsOccupied("test_map", 1, 2));

        // 新 footprint 占用
        Assert.True(ws.IsOccupied("test_map", 2, 1));
        Assert.True(ws.IsOccupied("test_map", 3, 1));
        Assert.True(ws.IsOccupied("test_map", 2, 2));
        Assert.True(ws.IsOccupied("test_map", 3, 2));
    }

    [Fact]
    public void WorldState_2x2Player_CannotReserveIntoOwnFootprint()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 1,
            GridY = 1,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });

        // 2x2 实体移动一格时新 footprint 必然与旧 footprint 重叠，应允许预约
        Assert.True(ws.TryReserveMove(1, "test_map", 1, 1, 1, 2, 300, 30, 40, 70));
    }

    [Fact]
    public void WorldState_2x2Player_BlocksOtherPlayerFromFootprint()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 1,
            GridY = 1,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 2,
            GridX = 4,
            GridY = 4,
            SizeX = 1,
            SizeY = 1,
            RoleName = "Small"
        });

        // 玩家 2 无法进入玩家 1 的 footprint
        Assert.False(ws.TryReserveMove(2, "test_map", 4, 4, 2, 1, 300, 30, 40, 70));
        Assert.False(ws.TryReserveMove(2, "test_map", 4, 4, 2, 2, 300, 30, 40, 70));

        // 玩家 2 可以走到远处
        Assert.True(ws.TryReserveMove(2, "test_map", 4, 4, 3, 3, 300, 30, 40, 70));
    }

    [Fact]
    public void WorldState_2x2Player_ReservedFootprintBlocksOthers()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 1,
            GridY = 1,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 2,
            GridX = 4,
            GridY = 4,
            SizeX = 1,
            SizeY = 1,
            RoleName = "Small"
        });

        // 玩家 1 预约移动到 (2,2)，新 footprint 为 (2,2),(3,2),(2,3),(3,3)
        Assert.True(ws.TryReserveMove(1, "test_map", 1, 1, 2, 2, 300, 30, 40, 70));

        // 玩家 2 无法进入玩家 1 预约的 footprint
        Assert.False(ws.TryReserveMove(2, "test_map", 4, 4, 3, 3, 300, 30, 40, 70));

        // 玩家 1 确认移动后，玩家 2 仍无法进入
        ws.ConfirmMove(1);
        Assert.False(ws.TryReserveMove(2, "test_map", 4, 4, 3, 3, 300, 30, 40, 70));
    }

    [Fact]
    public void WorldState_GetCombatPositions_ReturnsFootprint()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 1,
            GridY = 1,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });

        var positions = ws.GetCombatPositions(1);
        Assert.Equal(4, positions.Count);
        Assert.Contains(("test_map", 1, 1), positions);
        Assert.Contains(("test_map", 2, 1), positions);
        Assert.Contains(("test_map", 1, 2), positions);
        Assert.Contains(("test_map", 2, 2), positions);
    }

    [Fact]
    public void WorldState_GetCombatPositions_DualGrid_ReturnsUnion()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 1,
            GridY = 1,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });

        // 预约移动，强行让进度落在双格区间（手动改 StartTimeMs 已不行，这里用 Confirm 后检查）
        Assert.True(ws.TryReserveMove(1, "test_map", 1, 1, 2, 1, 300, 30, 40, 70));
        var res = ws.GetReservation(1);
        Assert.NotNull(res);
        // 让进度落在 DualStartRatio 和 DualEndRatio 之间
        res!.StartTimeMs = Environment.TickCount64 - 200;

        var positions = ws.GetCombatPositions(1);
        // 旧 footprint (1,1),(2,1),(1,2),(2,2) + 新 footprint (2,1),(3,1),(2,2),(3,2) 并集 = 6 格
        Assert.Equal(6, positions.Count);
    }

    [Fact]
    public void WorldState_2x2BuildingBlocksPlayerMove()
    {
        var mapData = CreateMapDataWithBuildingConfig();
        // 5x5 地图，(1,1) 放置 2x2 房舍
        var cells = Enumerable.Range(0, 25).Select(_ => "{\"terrain\":1,\"height\":0}").ToArray();
        cells[1 * 5 + 1] = "{\"terrain\":1,\"height\":0,\"decoration\":10000}";
        mapData.LoadMap("test_map", 0, 0, 5, 5, cells);

        var ws = CreateWorldState(mapData);
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 0,
            GridY = 0,
            SizeX = 1,
            SizeY = 1,
            RoleName = "Walker"
        });

        // 无法走进建筑 footprint
        Assert.False(ws.TryReserveMove(1, "test_map", 0, 0, 1, 1, 300, 30, 40, 70));
        Assert.False(ws.TryReserveMove(1, "test_map", 0, 0, 2, 2, 300, 30, 40, 70));

        // 可以走到 footprint 外
        Assert.True(ws.TryReserveMove(1, "test_map", 0, 0, 0, 1, 300, 30, 40, 70));
    }

    [Fact]
    public void WorldState_2x2Player_CannotMoveIntoBuilding()
    {
        var mapData = CreateMapDataWithBuildingConfig();
        var cells = Enumerable.Range(0, 25).Select(_ => "{\"terrain\":1,\"height\":0}").ToArray();
        cells[1 * 5 + 1] = "{\"terrain\":1,\"height\":0,\"decoration\":10000}";
        mapData.LoadMap("test_map", 0, 0, 5, 5, cells);

        var ws = CreateWorldState(mapData);
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 0,
            GridY = 0,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });

        // 2x2 玩家移动到 (1,0) 时 footprint 包含 (1,1) 建筑格，应失败
        Assert.False(ws.TryReserveMove(1, "test_map", 0, 0, 1, 0, 300, 30, 40, 70));

        // 移动到 (0,1) 时 footprint 包含 (1,2) 不是建筑格，但 (1,1) 仍是建筑格？
        // 锚点 (0,1) footprint = (0,1),(1,1),(0,2),(1,2) 包含 (1,1) 建筑格，应失败
        Assert.False(ws.TryReserveMove(1, "test_map", 0, 0, 0, 1, 300, 30, 40, 70));
    }

    [Fact]
    public void WorldState_CancelMove_ClearsReservedFootprint()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 1,
            GridY = 1,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 2,
            GridX = 4,
            GridY = 4,
            SizeX = 1,
            SizeY = 1,
            RoleName = "Small"
        });

        Assert.True(ws.TryReserveMove(1, "test_map", 1, 1, 2, 2, 300, 30, 40, 70));
        Assert.False(ws.TryReserveMove(2, "test_map", 4, 4, 3, 3, 300, 30, 40, 70));

        ws.CancelMove(1);

        // 取消预约后，玩家 2 可以进入
        Assert.True(ws.TryReserveMove(2, "test_map", 4, 4, 3, 3, 300, 30, 40, 70));
    }

    [Fact]
    public void MapDataProvider_FindNearestWalkableForFootprint_AvoidsBuilding()
    {
        var mapData = CreateMapDataWithBuildingConfig();
        // 5x5 地图，(1,1) 放置 2x2 房舍
        var cells = Enumerable.Range(0, 25).Select(_ => "{\"terrain\":1,\"height\":0}").ToArray();
        cells[1 * 5 + 1] = "{\"terrain\":1,\"height\":0,\"decoration\":10000}";
        mapData.LoadMap("test_map", 0, 0, 5, 5, cells);

        // 2x2 实体以 (1,1) 为锚点时落在建筑上，应被修正到最近的合法锚点
        var result = mapData.FindNearestWalkableForFootprint("test_map", 1, 1, 2, 2);
        Assert.NotNull(result);
        Assert.True(mapData.IsWalkable("test_map", result.Value.x, result.Value.y));

        // 修正后的锚点必须能让整个 footprint 可行走
        for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
                Assert.True(mapData.IsWalkable("test_map", result.Value.x + dx, result.Value.y + dy));
    }

    [Fact]
    public void WorldState_FindNearestWalkableForFootprint_AvoidsOccupiedCells()
    {
        var ws = CreateWorldState();
        ws.PlayerEnter("test_map", new MapPlayerState
        {
            AccountId = 1,
            GridX = 1,
            GridY = 1,
            SizeX = 2,
            SizeY = 2,
            RoleName = "Big"
        });

        // 要求 2x2 空地且不被其他实体占据；默认地图空旷，应返回原锚点
        var result = ws.FindNearestWalkableForFootprint("test_map", 0, 0, 2, 2, requireVacant: true, excludedEntityId: 1);
        Assert.NotNull(result);
        Assert.Equal((0, 0), result.Value);

        // 以 (1,1) 为锚点的 2x2 footprint 与玩家 1 重叠，应被修正
        result = ws.FindNearestWalkableForFootprint("test_map", 1, 1, 2, 2, requireVacant: true, excludedEntityId: 2);
        Assert.NotNull(result);
        Assert.NotEqual((1, 1), result.Value);
    }
}
