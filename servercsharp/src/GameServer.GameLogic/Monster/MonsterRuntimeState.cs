using GameServer.Tables;

namespace GameServer.Services.Monster;

public enum MonsterAiMode
{
    Overworld = 0,
    Combat = 1,
}

public enum MonsterState
{
    Idle,
    Patrol,
    Chase,
    Return,
    ForcedReturn,
    Combat,
    CombatHold,
    CombatChase,
    CombatRangedHold,
    CombatRangedChase,
    CombatCastHold,
    CombatCastChase,
    Dead,
}

/// <summary>
/// 怪物完整运行时状态 — 统一 MonsterState + AiConfig
/// </summary>
public class MonsterRuntimeState
{
    public long InstanceId { get; set; }
    public int MonsterId { get; set; }
    public string Name { get; set; } = "";
    public string MapName { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public int AiId { get; set; }
    public string AiType { get; set; } = "patrol";
    public AiConfig AiConfig { get; set; } = new();
    public MonsterState State { get; set; } = MonsterState.Idle;
    public long? TargetId { get; set; }
    public long? NarratedTargetId { get; set; }
    public HashSet<long> TerritoryPlayers { get; set; } = new();
    public long LastMoveTime { get; set; }
    public int Hp { get; set; } = 100;
    public int MaxHp { get; set; } = 100;
    public int Mp { get; set; } = 100;
    public int MaxMp { get; set; } = 100;
    public int Level { get; set; } = 1;
    // 战斗属性
    public int Patk { get; set; } = 10;
    public int Matk { get; set; } = 10;
    public int Pdef { get; set; } = 5;
    public int Mdef { get; set; } = 5;

    // ---- 移动系统（与玩家共用预占机制）----
    public int MoveSpeedMs { get; set; } = 800;   // 默认 800ms/格
    public bool IsMoving { get; set; } = false;
    public int MoveTargetX { get; set; }
    public int MoveTargetY { get; set; }
    public long MoveStartTime { get; set; }
    public bool CheckpointConfirmed { get; set; } = false;

    /// <summary>是否处于战斗中 — 战斗中的怪物不做 AI 决策和碰撞检测</summary>
    public bool InCombat { get; set; }
    public MonsterAiMode Mode => InCombat ? MonsterAiMode.Combat : MonsterAiMode.Overworld;

    // ---- 复活系统 ----
    public int RespawnTimeSec { get; set; }
    public ERespawnType RespawnType { get; set; }
    public int RespawnRange { get; set; }

    // ---- 领地与追击状态机（P1 新增）----
    /// <summary>追击超时计时器（秒）。目标离开领地后开始累计，达到 ChaseTimeout 后进入回归。</summary>
    public double ChaseTimeoutTimer { get; set; }
    /// <summary>当前回归目标巡逻点索引（-1=未设定）</summary>
    public int ReturnPatrolIndex { get; set; } = -1;
    /// <summary>回归完成后 Aggro 冷却期截止时间（TickCount64，0=无冷却）</summary>
    public long ReturnCooldownEndMs { get; set; }
    /// <summary>回归目标巡逻点坐标</summary>
    public (int x, int y)? ReturnPatrolPoint { get; set; }
}

public static class MonsterStateExtensions
{
    public static string ToStateString(this MonsterState state) => state switch
    {
        MonsterState.Idle => "idle",
        MonsterState.Patrol => "patrol",
        MonsterState.Chase => "chase",
        MonsterState.Return => "return",
        MonsterState.ForcedReturn => "forced_return",
        MonsterState.Combat => "combat",
        MonsterState.CombatHold => "combat_hold",
        MonsterState.CombatChase => "combat_chase",
        MonsterState.CombatRangedHold => "combat_ranged_hold",
        MonsterState.CombatRangedChase => "combat_ranged_chase",
        MonsterState.CombatCastHold => "combat_cast_hold",
        MonsterState.CombatCastChase => "combat_cast_chase",
        MonsterState.Dead => "dead",
        _ => state.ToString().ToLowerInvariant(),
    };
}

public class AiConfig
{
    public string AiType { get; set; } = "patrol";
    public int? PatrolRange { get; set; }
    public int? AggroRange { get; set; }
    public int? MaxChaseDistance { get; set; }
    public long? MoveIntervalMs { get; set; }
    public long? ChaseIntervalMs { get; set; }
    public CombatBehaviorType CombatBehavior { get; set; } = CombatBehaviorType.Auto;
    public int? CombatRange { get; set; }

    // ---- 领地与追击配置（P1 新增）----
    /// <summary>领地范围半径（格数）。0=无领地限制。</summary>
    public int? TerritoryRadius { get; set; }
    /// <summary>追击超时时间（秒）。目标离开领地后，超过此时间未追上则进入回归。</summary>
    public double? ChaseTimeout { get; set; }
    /// <summary>巡逻点坐标列表（策划手动配置）。为空时在 Spawn 周围随机巡逻。</summary>
    public List<(int x, int y)>? PatrolPoints { get; set; }
    /// <summary>回归时移速倍率（相对于正常移速）。默认 1.5。</summary>
    public double? ReturnSpeedMultiplier { get; set; }
    /// <summary>回归 Buff ID。0=无回归 Buff。</summary>
    public int? ReturnBuffId { get; set; }
}
