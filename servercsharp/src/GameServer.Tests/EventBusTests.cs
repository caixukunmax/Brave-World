using GameServer.Common.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.Tests;

public class EventBusTests
{
    [Fact]
    public void Emit_With_No_Subscribers_Does_Not_Throw()
    {
        var bus = new EventBus(NullLogger<EventBus>.Instance);
        bus.Emit("missing");
    }

    [Fact]
    public void Emit_Invokes_Subscriber()
    {
        var bus = new EventBus(NullLogger<EventBus>.Instance);
        object? received = null;
        bus.On("test", data => received = data);

        bus.Emit("test", 42);

        Assert.Equal(42, received);
    }

    [Fact]
    public void Subscriber_Exception_Does_Not_Break_Others()
    {
        var bus = new EventBus(NullLogger<EventBus>.Instance);
        var received = new List<int>();
        bus.On("test", _ => throw new InvalidOperationException("boom"));
        bus.On("test", _ => received.Add(1));

        bus.Emit("test");

        Assert.Single(received);
    }

    [Fact]
    public void Concurrent_Subscribe_And_Emit_Does_Not_Throw()
    {
        var bus = new EventBus(NullLogger<EventBus>.Instance);
        var received = 0;
        bus.On("test", _ => Interlocked.Increment(ref received));

        var tasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() =>
            {
                if (i % 2 == 0)
                    bus.On("test", _ => Interlocked.Increment(ref received));
                else
                    bus.Emit("test");
            }))
            .ToArray();

        Assert.All(tasks, t => t.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Off_Removes_Subscriber()
    {
        var bus = new EventBus(NullLogger<EventBus>.Instance);
        var called = false;
        Action<object?> handler = _ => called = true;
        bus.On("test", handler);
        bus.Off("test", handler);

        bus.Emit("test");

        Assert.False(called);
    }
}
