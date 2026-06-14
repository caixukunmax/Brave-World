namespace GameServer.Services.Core;

/// <summary>
/// 游戏逻辑调度器 — 所有游戏状态变更统一由此单线程串行执行，避免多线程竞争。
/// </summary>
public interface IGameLoopScheduler
{
    /// <summary>
    /// 入队一个无返回值的动作。
    /// </summary>
    void Enqueue(Func<ValueTask> action);

    /// <summary>
    /// 入队一个有返回值的动作，并可异步等待结果。
    /// </summary>
    ValueTask<T> Enqueue<T>(Func<ValueTask<T>> action);

    /// <summary>
    /// 入队同步形式的无返回值动作（便捷方法）。
    /// </summary>
    void Enqueue(Action action);

    /// <summary>
    /// 入队同步形式的有返回值动作（便捷方法）。
    /// </summary>
    ValueTask<T> Enqueue<T>(Func<T> action);

    /// <summary>
    /// 启动调度器消费循环。
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记完成并等待队列中剩余动作处理完毕。
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
