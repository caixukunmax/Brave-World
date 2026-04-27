namespace GameServer.Services.Monster;

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
    public string State { get; set; } = "idle";
    public long? TargetId { get; set; }
    public long LastMoveTime { get; set; }
    public int Hp { get; set; } = 100;
    public int MaxHp { get; set; } = 100;
    public int Level { get; set; } = 1;
    // 战斗属性
    public int Patk { get; set; } = 10;
    public int Matk { get; set; } = 10;
    public int Pdef { get; set; } = 5;
    public int Mdef { get; set; } = 5;
    public int Agility { get; set; } = 100;

    // ---- 移动系统（与玩家共用预占机制）----
    public int MoveSpeedMs { get; set; } = 800;   // 默认 800ms/格
    public bool IsMoving { get; set; } = false;
    public int MoveTargetX { get; set; }
    public int MoveTargetY { get; set; }
    public long MoveStartTime { get; set; }
    public bool CheckpointConfirmed { get; set; } = false;

    /// <summary>是否处于战斗中 — 战斗中的怪物不做 AI 决策和碰撞检测</summary>
    public bool InCombat { get; set; }
}

public class AiConfig
{
    public string AiType { get; set; } = "patrol";
    public int? PatrolRange { get; set; }
    public int? AggroRange { get; set; }
    public int? MaxChaseDistance { get; set; }
    public long? MoveIntervalMs { get; set; }
    public long? ChaseIntervalMs { get; set; }
}
