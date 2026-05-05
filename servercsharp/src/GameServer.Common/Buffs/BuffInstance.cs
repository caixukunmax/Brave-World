namespace GameServer.Common.Buffs;

/// <summary>
/// Buff 实例 — 挂在实体上的运行时状态
/// </summary>
public class BuffInstance
{
    public int BuffId { get; set; }              // 配置 ID
    public long CasterId { get; set; }           // 施法者 ID
    public long TargetId { get; set; }           // 挂载目标 ID
    public int Stacks { get; set; } = 1;         // 叠加层数
    public long ApplyTime { get; set; }          // 施加时刻（ms）
    public long ExpireTime { get; set; }         // 过期时刻（ms），0=永久
    public long LastTickTime { get; set; }       // 上次 tick 时刻（ms）
    public int TickCount { get; set; }           // 已 tick 次数
    public int ShieldRemaining { get; set; }     // 护盾剩余吸收量
    public int SnapshotAtk { get; set; }         // 施加时快照物攻（用于 DOT 系数计算）
    public int SnapshotMatk { get; set; }       // 施加时快照魔攻（用于 DOT 系数计算）
}
