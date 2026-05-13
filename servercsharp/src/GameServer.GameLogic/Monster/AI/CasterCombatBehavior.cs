using GameServer.Tables;
using GameServer.Common.Config;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 施法型战斗行为：当前阶段和远程一样先追到施法距离，后续再扩展风筝/后撤�?/// </summary>
public class CasterCombatBehavior : CombatBehaviorBase
{
    public CasterCombatBehavior(MapDataProvider mapData) : base(mapData) { }

    public override CombatBehaviorType Type => CombatBehaviorType.Caster;

    protected override int ResolveEngageRange(MonsterRuntimeState monster)
        => Math.Max(1, monster.AiConfig.CombatRange ?? 1);

    protected override string HoldState => "combat_cast_hold";
    protected override string ChaseState => "combat_cast_chase";
}
