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
}

public class CombatContext
{
    public long EntityId { get; set; }
    public string State { get; set; } = "IDLE";
    public string SubState { get; set; } = "NONE";
    public double AtbValue { get; set; }
    public double AtbBoost { get; set; }
    public int AtbBoostStacks { get; set; }
    public int? CastSkillId { get; set; }
    public long? CastEndTime { get; set; }
    public long? PostCastEndTime { get; set; }
    public HashSet<int> RelationIds { get; set; } = new();
    public Dictionary<int, long> SkillCooldowns { get; set; } = new();
    public string Job { get; set; } = "";
    public List<int> SkillPool { get; set; } = new();
    public int PreferredSkillId { get; set; }  // 优先释放的技能 ID（0=无优先）
    public BuffContainer Buffs { get; set; } = new();
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
