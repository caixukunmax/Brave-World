using GameServer.Common.Buffs;
using PGame = global::Game;

namespace GameServer.Services.Map.Combat;

public class CombatRelation
{
    public int RelationId { get; set; }
    public long AttackerId { get; set; }
    public long TargetId { get; set; }
    public long StartTime { get; set; }
    public long LastDamageTime { get; set; }
    public int LastDistance { get; set; }
    public bool IsActive { get; set; }
    public double DisengageTimer1 { get; set; }
    public double DisengageTimer2 { get; set; }
    /// <summary>挑衅期结束时间戳（毫秒）。最近一次碰撞/被碰撞该实体的攻击者的挑衅截止时间。</summary>
    public long TauntEndTime { get; set; }
    /// <summary>挑衅者ID（最近一次碰撞该关系目标的发起者）</summary>
    public long TauntSourceId { get; set; }
    /// <summary>累计伤害（该关系方向上的总伤害）</summary>
    public int AccumulatedDamage { get; set; }
}

public class CombatContext
{
    public long EntityId { get; set; }
    public string State { get; set; } = "IDLE";
    public string SubState { get; set; } = "NONE";
    public int? CastSkillId { get; set; }
    public long? CastEndTime { get; set; }
    public long? PostCastEndTime { get; set; }
    public HashSet<int> RelationIds { get; set; } = new();
    public Dictionary<int, long> SkillCooldowns { get; set; } = new();
    public string Job { get; set; } = "";
    public List<int> SkillPool { get; set; } = new();
    public int PreferredSkillId { get; set; }  // 优先释放的技能 ID（0=无优先）
    public BuffContainer Buffs { get; set; } = new();
    /// <summary>先攻加速值（0~100，百分比）。碰撞时设置，第一个技能使用后清零。</summary>
    public int FirstStrikeHaste { get; set; }
    /// <summary>是否已使用先手提速。false=未使用，true=已使用（首个技能已释放）。</summary>
    public bool HasUsedFirstStrike { get; set; }
    /// <summary>优先攻击目标ID（玩家手动锁定）。0=无优先目标。</summary>
    public long PriorityTargetId { get; set; }
}

public class CombatLogEntry
{
    public PGame.CombatLogType LogType { get; set; }
    public ulong Timestamp { get; set; }
    public string ActorName { get; set; } = "";
    public string TargetName { get; set; } = "";
    public string SkillName { get; set; } = "";
    public int Value { get; set; }
    public string Extra { get; set; } = "";
    public long ActorId { get; set; }
    public string? MapName { get; set; }
}
