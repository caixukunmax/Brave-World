namespace GameServer.Services.Core;

/// <summary>
/// 怪物 AI 服务接口 — 宿主通过此接口调用怪物逻辑
/// </summary>
public interface IMonsterAiService
{
    void Init();
    void Tick();
    void OnDamage(long instanceId, long attackerId, int damage);
    void OnRegen(long instanceId, int regen);
    bool IsOccupied(string mapName, int x, int y);
    List<MapMonsterState> GetMonstersOnMap(string mapName);
}
