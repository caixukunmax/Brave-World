using GameServer.Database.Models;
using GameServer.Services.Player;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.Tests;

public class PlayerSessionManagerTests
{
    private static PlayerSessionManager CreateManager() =>
        new(null!, null!, null!, null!, NullLogger<PlayerSessionManager>.Instance);

    [Fact]
    public void SetOnline_And_TryGetPlayer_Roundtrip()
    {
        var mgr = CreateManager();
        var role = new Role { AccountId = 1 };

        mgr.SetOnline(1, role);

        Assert.True(mgr.TryGetPlayer(1, out var found));
        Assert.Equal(role, found);
    }

    [Fact]
    public void SetOffline_Removes_Player()
    {
        var mgr = CreateManager();
        mgr.SetOnline(1, new Role { AccountId = 1 });

        Assert.True(mgr.SetOffline(1));
        Assert.False(mgr.TryGetPlayer(1, out _));
    }

    [Fact]
    public void OnlinePlayers_Returns_Snapshot()
    {
        var mgr = CreateManager();
        mgr.SetOnline(1, new Role { AccountId = 1 });

        var snapshot = mgr.OnlinePlayers;
        mgr.SetOffline(1);

        Assert.Single(snapshot);
        Assert.False(mgr.TryGetPlayer(1, out _));
    }

    [Fact]
    public void Concurrent_SetOnline_And_TryGetPlayer_Does_Not_Throw()
    {
        var mgr = CreateManager();
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
            {
                mgr.SetOnline(i, new Role { AccountId = i });
                mgr.TryGetPlayer(i, out _);
                mgr.SetOffline(i);
            }))
            .ToArray();

        Assert.All(tasks, t => t.Wait(TimeSpan.FromSeconds(5)));
    }
}
