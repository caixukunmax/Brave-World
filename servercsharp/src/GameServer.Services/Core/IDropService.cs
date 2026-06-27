using PGame = global::Game;

namespace GameServer.Services.Core;

/// <summary>
/// 掉落物服务接口 — 由 GameServer.GameLogic.Drop.DropManager 实现。
/// 通过 IDropService 暴露给宿主（GameServer/Program）进行 tick 调度，
/// 并暴露给消息 handler 进行自动拾取与进地图同步。
/// </summary>
public interface IDropService
{
    /// <summary>每帧更新：归属锁定倒计时 + 超时清理。</summary>
    void Tick(float dt);

    /// <summary>玩家移动到指定格子后，尝试自动拾取该格掉落物。</summary>
    Task TryAutoPickup(long playerId, string mapName, int x, int y);

    /// <summary>获取地图上当前所有掉落物（用于 MapInfoSyncNotify）。</summary>
    List<PGame.DropItemInfo> GetDrops(string mapName);
}
