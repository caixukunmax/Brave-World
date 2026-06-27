using GameServer.Database.Models;
using GameServer.GameLogic.Inventory;
using Xunit;

namespace GameServer.Tests;

public class InventoryOrderHelperTests
{
    [Fact]
    public void ApplyOrder_MovesItemsByOrder()
    {
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1, Count = 10 },
            new() { ItemId = 2, Count = 20 },
            new() { ItemId = 3, Count = 30 },
        };
        var order = new List<int> { 3, 1, 2 };

        var result = InventoryOrderHelper.ApplyOrder(items, order);

        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[0].ItemId);
        Assert.Equal(1, result[1].ItemId);
        Assert.Equal(2, result[2].ItemId);
    }

    [Fact]
    public void ApplyOrder_AppendsUnknownItemsToEnd()
    {
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1, Count = 10 },
            new() { ItemId = 2, Count = 20 },
            new() { ItemId = 3, Count = 30 },
            new() { ItemId = 4, Count = 40 },
        };
        var order = new List<int> { 3, 1 };

        var result = InventoryOrderHelper.ApplyOrder(items, order);

        Assert.Equal(4, result.Count);
        Assert.Equal(3, result[0].ItemId);
        Assert.Equal(1, result[1].ItemId);
        Assert.Equal(2, result[2].ItemId);
        Assert.Equal(4, result[3].ItemId);
    }

    [Fact]
    public void AppendItem_AddsWhenAbsent()
    {
        var order = new List<int> { 1, 2 };

        InventoryOrderHelper.AppendItem(order, 3);

        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public void AppendItem_IgnoresWhenPresent()
    {
        var order = new List<int> { 1, 2 };

        InventoryOrderHelper.AppendItem(order, 1);

        Assert.Equal(new[] { 1, 2 }, order);
    }

    [Fact]
    public void RemoveItem_RemovesWhenPresent()
    {
        var order = new List<int> { 1, 2, 3 };

        InventoryOrderHelper.RemoveItem(order, 2);

        Assert.Equal(new[] { 1, 3 }, order);
    }

    [Fact]
    public void RemoveItem_DoesNothingWhenAbsent()
    {
        var order = new List<int> { 1, 2 };

        InventoryOrderHelper.RemoveItem(order, 3);

        Assert.Equal(new[] { 1, 2 }, order);
    }
}
