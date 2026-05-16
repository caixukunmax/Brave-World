using GameServer.Services.Core;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// AI 行为接口 — 移植自 ai/behavior/*.lua
/// </summary>
public interface IBehaviorHandler
{
    /// <summary>
    /// 执行 AI 决策，返回下一步坐标 (nx, ny) 或 null
    /// </summary>
    /// <param name="world">世界状态，用于查询实体占据（寻路绕开动态障碍）</param>
    (int x, int y)? Run(MonsterRuntimeState m, string mapName, Dictionary<long, PlayerStateView> players, IWorldState? world);
}

/// <summary>
/// 玩家位置视图（供 AI 查询）
/// </summary>
public class PlayerStateView
{
    public long AccountId { get; set; }
    public long RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public int GridX { get; set; }
    public int GridY { get; set; }
}
