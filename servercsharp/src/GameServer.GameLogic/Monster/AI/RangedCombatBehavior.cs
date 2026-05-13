using GameServer.Tables;
using GameServer.Common.Config;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 远程战斗行为：追到最远可攻击距离后停下�?/// </summary>
public class RangedCombatBehavior : CombatBehaviorBase
{
    public RangedCombatBehavior(MapDataProvider mapData) : base(mapData) { }

    public override CombatBehaviorType Type => CombatBehaviorType.Ranged;

    protected override int ResolveEngageRange(MonsterRuntimeState monster)
        => Math.Max(1, monster.AiConfig.CombatRange ?? 1);

    protected override string HoldState => "combat_ranged_hold";
    protected override string ChaseState => "combat_ranged_chase";
}
