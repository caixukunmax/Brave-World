using GameServer.Services.Map;

namespace GameServer.Services.Core;

/// <summary>
/// 战斗服务接口 — 宿主通过此接口调用战斗逻辑
/// </summary>
public interface ICombatService
{
    void OnCollision(long entityA, long entityB, Dictionary<string, MapState> maps);
    void Tick(double dt, Dictionary<string, MapState> maps, IMonsterRegistry? monsterRegistry);
    bool IsCasting(long entityId);

    /// <summary>玩家手动/中断施法请求 — 纯 CD 即时制</summary>
    byte[]? HandleCastRequest(long playerId, int skillId, bool interrupt, long? targetId, Dictionary<string, MapState> maps);
}
