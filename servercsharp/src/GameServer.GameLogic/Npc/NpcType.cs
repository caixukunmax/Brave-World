namespace GameServer.GameLogic.Npc;

/// <summary>
/// NPC 类型枚举
/// </summary>
public enum NpcType
{
    None = 0,
    JobMaster = 1,    // 转职大师
    Combatant = 2,    // 可战斗NPC（对话触发战斗）
}
