namespace GameServer.Services.Core;

/// <summary>
/// 游戏常量集中管理 — 替代散落各文件的硬编码值
/// 部分字段支持从 JSON 配置覆盖
/// </summary>
public static class GameConstants
{
    // ---- 地图 ----
    public const string DefaultMapName = "xinshoucun";
    public const int DefaultMapId = 1;
    public const int DefaultSpawnX = 25;
    public const int DefaultSpawnY = 25;
    public const int DefaultMapSpawnX = DefaultSpawnX;
    public const int DefaultMapSpawnY = DefaultSpawnY;

    // ---- 创角初始配置 ----
    public const int StartGold = 10000;
    public const int StartDiamond = 100;
    public const int StartTotalPower = 100;
    public const int MaxRoleCount = 3;
    public const string InitItems = "1001:1,1002:10,2001:100";

    // ---- 基础战斗属性 ----
    public static readonly (int Hp, int Mp, int Agi, int Patk, int Matk, int Pdef, int Mdef) BasePlayerAttrs = (BaseHp, BaseMp, BaseAgi, BasePatk, BaseMatk, BasePdef, BaseMdef);
    public const int BaseHp = 100;
    public const int BaseMp = 50;
    public const int BaseAgi = 100;
    public const int BasePatk = 10;
    public const int BaseMatk = 10;
    public const int BasePdef = 5;
    public const int BaseMdef = 5;
    public const int BaseMoveSpeedMs = 540;
    public const int MinMoveSpeedMs = 120;
    public const int MaxMoveSpeedMs = 600;

    // ---- ATB 战斗 ----
    public const double BaseAtbRate = 10;
    public const double AtbBoostPerMiss = 0.25;
    public const double AtbBoostMax = 0.50;
    public const int AtbBoostStacksMax = 2;
    public const double AtbBoostDecayRate = 0.05; // boost 每秒衰减 0.05

    // ---- 脱战 ----
    // 距离A：跑出X格后，Y秒内无伤害，再等Z秒脱战
    public const int DisengageDistanceA = 5;
    public const double DisengageNoDamageTimeT1 = 2;
    public const double DisengageTimeS1 = 3;
    // 距离B：跑出X格后，等Y秒直接脱战
    public const int DisengageDistanceB = 10;
    public const double DisengageTimeS2 = 2;

    // ---- 怪物 ----
    public const int DefaultMonsterMoveSpeedMs = 800;

    // ---- 怪物回血 ----
    public const double MonsterRegenPercentPerSec = 0.05;

    // ---- 玩家 HP 恢复 ----
    /// <summary>脱战后 HP 恢复倍率（基于 MaxHp 百分比）</summary>
    public const double PlayerHpRegenPercentPerSec = 0.05; // 5%/秒，脱战20秒回满

    // ---- 玩家 MP 恢复 ----
    /// <summary>战斗中 MP 恢复倍率（基于 mp_regen 属性）</summary>
    public const double CombatMpRegenMultiplier = 1.0;
    /// <summary>脱战后 MP 恢复倍率（基于 mp_regen 属性）</summary>
    public const double OutOfCombatMpRegenMultiplier = 3.0;
    /// <summary>无 mp_regen 属性时的默认每秒恢复百分比（MaxMp 的百分比）</summary>
    public const double DefaultMpRegenPercentPerSec = 0.02;

    // ---- 移动系统（可被 JSON 覆盖）----
    public static int MoveCheckRatio { get; set; } = 30;
    public static int MoveDualGridStartRatio { get; set; } = 30;
    public static int MoveDualGridEndRatio { get; set; } = 70;

    /// <summary>从 JSON 配置覆盖常量（程序启动时调用一次）</summary>
    public static void LoadFromConfig()
    {
        // MoveSystem 常量现在使用默认值
        // 后续可迁移到 Luban GlobalConfig 表
    }

    // ---- Tick 间隔 ----
    public const int CombatTickMs = 100;
    public const int MonsterAiTickMs = 500;
    public const int HeartbeatCheckSec = 30;

    // ---- 玩家池 ----
    public const int PoolCount = 1; // 单进程无需分片
}
