namespace GameServer.Services.Core;

/// <summary>
/// 怪物注册接口 — 供 Combat 回调怪物伤害/回血/脱战
/// </summary>
public interface IMonsterRegistry
{
    void OnDamage(long instanceId, long attackerId, int damage);
    void OnRegen(long instanceId, int regen);
    bool IsOccupied(string mapName, int x, int y);
    /// <summary>怪物脱战，恢复 AI 行为</summary>
    void OnDisengage(long instanceId);
}
