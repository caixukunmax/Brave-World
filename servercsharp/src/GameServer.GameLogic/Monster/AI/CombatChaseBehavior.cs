using GameServer.Tables;
using GameServer.Common.Config;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 近战战斗行为：追到贴身后等待战斗系统出手�?/// 兼容第一阶段实现与现有测试�?/// </summary>
public class CombatChaseBehavior : CombatBehaviorBase
{
    public CombatChaseBehavior(MapDataProvider mapData) : base(mapData) { }

    public override CombatBehaviorType Type => CombatBehaviorType.Melee;
    protected override int ResolveEngageRange(MonsterRuntimeState monster) => 1;
    protected override MonsterState HoldState => MonsterState.CombatHold;
    protected override MonsterState ChaseState => MonsterState.CombatChase;
}
