using GameServer.Tables;
using GameServer.Services.Core;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 战斗态怪物行为接口�?/// 与大地图巡�?AI 分层，专门处理进入战斗后的追击与贴身逻辑�?/// </summary>
public interface ICombatBehaviorHandler
{
    CombatBehaviorType Type { get; }
    (int x, int y)? Run(MonsterRuntimeState m, string mapName, Dictionary<long, PlayerStateView> players, IWorldState? world);
}
