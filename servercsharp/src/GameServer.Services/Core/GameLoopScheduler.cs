using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Core;

/// <summary>
/// 游戏逻辑调度器实现 — 基于 Channel 的单消费者循环。
/// 所有游戏状态变更通过 Enqueue 入队，由单一逻辑线程串行执行。
/// </summary>
public sealed class GameLoopScheduler : IGameLoopScheduler, IDisposable
{
    private readonly ILogger<GameLoopScheduler> _logger;
    private readonly Channel<SchedulerWorkItem> _channel;
    private readonly CancellationTokenSource _cts;
    private Task? _consumerTask;

    public GameLoopScheduler(ILogger<GameLoopScheduler> logger)
    {
        _logger = logger;
        _channel = Channel.CreateUnbounded<SchedulerWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _cts = new CancellationTokenSource();
    }

    public void Enqueue(Func<ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _channel.Writer.TryWrite(new AsyncVoidWorkItem(action));
    }

    public ValueTask<T> Enqueue<T>(Func<ValueTask<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _channel.Writer.TryWrite(new AsyncResultWorkItem<T>(action, tcs));
        return new ValueTask<T>(tcs.Task);
    }

    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _channel.Writer.TryWrite(new SyncVoidWorkItem(action));
    }

    public ValueTask<T> Enqueue<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _channel.Writer.TryWrite(new SyncResultWorkItem<T>(action, tcs));
        return new ValueTask<T>(tcs.Task);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_consumerTask != null)
            return Task.CompletedTask;

        _consumerTask = Task.Run(() => RunConsumerAsync(_cts.Token), cancellationToken);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _channel.Writer.Complete();
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignored
        }

        if (_consumerTask != null)
        {
            try
            {
                await _consumerTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // caller requested cancellation
            }
        }
    }

    private async Task RunConsumerAsync(CancellationToken ct)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await item.ExecuteAsync(_logger, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameLoopScheduler] Unexpected error in consumer loop");
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
            _cts.Dispose();
            _channel.Writer.TryComplete();
        }
        catch
        {
            // ignored
        }
    }

    private abstract class SchedulerWorkItem
    {
        public abstract ValueTask ExecuteAsync(ILogger logger, CancellationToken ct);
    }

    private sealed class AsyncVoidWorkItem : SchedulerWorkItem
    {
        private readonly Func<ValueTask> _action;

        public AsyncVoidWorkItem(Func<ValueTask> action)
        {
            _action = action;
        }

        public override async ValueTask ExecuteAsync(ILogger logger, CancellationToken ct)
        {
            try
            {
                await _action();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[GameLoopScheduler] Error executing async action");
            }
        }
    }

    private sealed class AsyncResultWorkItem<T> : SchedulerWorkItem
    {
        private readonly Func<ValueTask<T>> _action;
        private readonly TaskCompletionSource<T> _tcs;

        public AsyncResultWorkItem(Func<ValueTask<T>> action, TaskCompletionSource<T> tcs)
        {
            _action = action;
            _tcs = tcs;
        }

        public override async ValueTask ExecuteAsync(ILogger logger, CancellationToken ct)
        {
            try
            {
                _tcs.TrySetResult(await _action());
            }
            catch (OperationCanceledException)
            {
                _tcs.TrySetCanceled(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[GameLoopScheduler] Error executing async result action");
                _tcs.TrySetException(ex);
            }
        }
    }

    private sealed class SyncVoidWorkItem : SchedulerWorkItem
    {
        private readonly Action _action;

        public SyncVoidWorkItem(Action action)
        {
            _action = action;
        }

        public override ValueTask ExecuteAsync(ILogger logger, CancellationToken ct)
        {
            try
            {
                _action();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[GameLoopScheduler] Error executing sync action");
            }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SyncResultWorkItem<T> : SchedulerWorkItem
    {
        private readonly Func<T> _action;
        private readonly TaskCompletionSource<T> _tcs;

        public SyncResultWorkItem(Func<T> action, TaskCompletionSource<T> tcs)
        {
            _action = action;
            _tcs = tcs;
        }

        public override ValueTask ExecuteAsync(ILogger logger, CancellationToken ct)
        {
            try
            {
                _tcs.TrySetResult(_action());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[GameLoopScheduler] Error executing sync result action");
                _tcs.TrySetException(ex);
            }
            return ValueTask.CompletedTask;
        }
    }
}
