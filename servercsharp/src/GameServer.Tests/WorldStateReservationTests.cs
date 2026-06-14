using GameServer.Common.Config;
using GameServer.Services.Core;
using GameServer.Services.World;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.Tests;

public class WorldStateReservationTests
{
    private static WorldState CreateWorldState()
    {
        var mapData = new MapDataProvider();
        // 3x3 地图，所有格子可行走（terrain=1，无配置时默认可走）
        var cells = Enumerable.Range(0, 9).Select(_ => "{\"terrain\":1,\"height\":0}").ToArray();
        mapData.LoadMap("test_map", 0, 0, 3, 3, cells);
        return new WorldState(mapData, NullLogger<WorldState>.Instance);
    }

    [Fact]
    public void TryReserveMove_Reserves_Cell()
    {
        var ws = CreateWorldState();
        bool ok = ws.TryReserveMove(1, "test_map", 0, 0, 1, 0, 300, 30, 40, 70);

        Assert.True(ok);
        Assert.True(ws.IsOccupied("test_map", 1, 0));
    }

    [Fact]
    public void TryReserveMove_Target_Cell_Already_Reserved_Returns_False()
    {
        var ws = CreateWorldState();
        ws.TryReserveMove(1, "test_map", 0, 0, 1, 0, 300, 30, 40, 70);

        bool ok = ws.TryReserveMove(2, "test_map", 0, 1, 1, 0, 300, 30, 40, 70);

        Assert.False(ok);
    }

    [Fact]
    public void CancelMove_Releases_Cell()
    {
        var ws = CreateWorldState();
        ws.TryReserveMove(1, "test_map", 0, 0, 1, 0, 300, 30, 40, 70);
        ws.CancelMove(1);

        Assert.False(ws.IsOccupied("test_map", 1, 0));
    }

    [Fact]
    public void CompleteMove_Releases_Cell()
    {
        var ws = CreateWorldState();
        ws.TryReserveMove(1, "test_map", 0, 0, 1, 0, 300, 30, 40, 70);
        ws.CompleteMove(1);

        Assert.False(ws.IsOccupied("test_map", 1, 0));
    }

    [Fact]
    public void ReReserve_Move_Cancels_Old_Reservation()
    {
        var ws = CreateWorldState();
        ws.TryReserveMove(1, "test_map", 0, 0, 1, 0, 300, 30, 40, 70);
        ws.TryReserveMove(1, "test_map", 1, 0, 2, 0, 300, 30, 40, 70);

        Assert.False(ws.IsOccupied("test_map", 1, 0));
        Assert.True(ws.IsOccupied("test_map", 2, 0));
    }

    [Fact]
    public void Concurrent_Reservations_Are_Consistent()
    {
        var ws = CreateWorldState();
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
            {
                long entityId = i + 1;
                bool reserved = ws.TryReserveMove(entityId, "test_map", 0, 0, 1, 0, 300, 30, 40, 70);
                if (reserved)
                    ws.CancelMove(entityId);
            }))
            .ToArray();

        Assert.All(tasks, t => t.Wait(TimeSpan.FromSeconds(5)));

        // 所有预约都应已被取消，最终无占用
        Assert.False(ws.IsOccupied("test_map", 1, 0));
    }
}
