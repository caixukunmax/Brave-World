namespace GameServer.Services.Map.Combat;

/// <summary>
/// 战斗系统常量 — 集中管理魔法数字
/// </summary>
public static class CombatConstants
{
    /// <summary>怪物 InstanceId 起始值（>= 此值为怪物）</summary>
    public const long MonsterIdThreshold = 1_000_000;

    /// <summary>ATB 满值（行动条充满时可以行动）</summary>
    public const int AtbMax = 100;

    /// <summary>默认 NPC 战斗属性</summary>
    public static class DefaultNpcCombat
    {
        public const int MaxHp = 500;
        public const int Hp = 500;
        public const int MaxMp = 100;
        public const int Mp = 100;
        public const int Patk = 20;
        public const int Matk = 10;
        public const int Pdef = 15;
        public const int Mdef = 10;
        public const int Agility = 120;
    }
}