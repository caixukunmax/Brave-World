using GameServer.Services.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.Tests;

public class GameLoopSchedulerTests
{
    [Fact]
    public async Task Enqueue_Actions_Execute_In_Order()
    {
        var scheduler = new GameLoopScheduler(NullLogger<GameLoopScheduler>.Instance);
        await scheduler.StartAsync();

        var results = new List<int>();
        scheduler.Enqueue(() => results.Add(1));
        scheduler.Enqueue(() => results.Add(2));
        scheduler.Enqueue(() => results.Add(3));

        await scheduler.Enqueue(() => results.Count);

        Assert.Equal(new[] { 1, 2, 3 }, results);
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Enqueue_With_Result_Returns_Value()
    {
        var scheduler = new GameLoopScheduler(NullLogger<GameLoopScheduler>.Instance);
        await scheduler.StartAsync();

        var result = await scheduler.Enqueue(() => 42);

        Assert.Equal(42, result);
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Async_Enqueue_Awaited_In_Order()
    {
        var scheduler = new GameLoopScheduler(NullLogger<GameLoopScheduler>.Instance);
        await scheduler.StartAsync();

        var results = new List<int>();
        scheduler.Enqueue(async () =>
        {
            await Task.Yield();
            results.Add(1);
        });
        scheduler.Enqueue(() => results.Add(2));

        await scheduler.Enqueue(async () =>
        {
            await Task.Yield();
            return results.Count;
        });

        Assert.Equal(new[] { 1, 2 }, results);
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Exception_In_One_Action_Does_Not_Stop_Scheduler()
    {
        var scheduler = new GameLoopScheduler(NullLogger<GameLoopScheduler>.Instance);
        await scheduler.StartAsync();

        var tcs = new TaskCompletionSource<bool>();
        scheduler.Enqueue(() => throw new InvalidOperationException("boom"));
        scheduler.Enqueue(() =>
        {
            tcs.SetResult(true);
            return ValueTask.CompletedTask;
        });

        await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.True(tcs.Task.IsCompletedSuccessfully, "Scheduler should continue after exception");
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Multiple_Concurrent_Enqueues_Are_Serialized()
    {
        var scheduler = new GameLoopScheduler(NullLogger<GameLoopScheduler>.Instance);
        await scheduler.StartAsync();

        var results = new List<int>();
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => scheduler.Enqueue(() => results.Add(i))))
            .ToArray();

        await Task.WhenAll(tasks);
        await scheduler.Enqueue(() => results.Count);

        Assert.Equal(100, results.Count);
        await scheduler.StopAsync();
    }
}
