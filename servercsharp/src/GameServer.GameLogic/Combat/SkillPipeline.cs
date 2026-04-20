using GameServer.Services.Map.Combat.Actions;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 技能管线：6 阶段技能释放流程 — 移植自 combat/pipeline.lua
/// </summary>
public class SkillPipeline
{
    private readonly ILogger _logger;
    private readonly ActionRegistry _actionRegistry;
    public CombatManager? CombatManager { get; set; }

    public SkillPipeline(ILogger<SkillPipeline> logger, ActionRegistry actionRegistry)
    {
        _logger = logger;
        _actionRegistry = actionRegistry;
    }

    // ---- 技能配置 ----
    private static readonly Dictionary<int, SkillConfig> SkillConfigs = new()
    {
        [1] = new SkillConfig
        {
            Id = 1,
            Name = "普通攻击",
            CastRange = 1,
            CastTime = 0.5,
            InterruptOnMove = false,
            PostCastTime = 0.1,
            Cooldown = 0,
            MpCost = 0,
            TargetType = "SingleEnemy",
            Actions =
            [
                new Dictionary<string, object> { ["type"] = "DealDamage", ["damageType"] = "physical", ["coefficient"] = 1.0 },
            ],
        },
        [2] = new SkillConfig
        {
            Id = 2,
            Name = "烈斩",
            CastRange = 1,
            CastTime = 0.8,
            InterruptOnMove = false,
            PostCastTime = 0.1,
            Cooldown = 3,
            MpCost = 10,
            TargetType = "SingleEnemy",
            Actions =
            [
                new Dictionary<string, object> { ["type"] = "DealDamage", ["damageType"] = "physical", ["coefficient"] = 1.5 },
            ],
        },
        [3] = new SkillConfig
        {
            Id = 3,
            Name = "盾击",
            CastRange = 1,
            CastTime = 0.6,
            InterruptOnMove = false,
            PostCastTime = 0.1,
            Cooldown = 5,
            MpCost = 8,
            TargetType = "SingleEnemy",
            Actions =
            [
                new Dictionary<string, object> { ["type"] = "DealDamage", ["damageType"] = "physical", ["coefficient"] = 0.8 },
                new Dictionary<string, object> { ["type"] = "InterruptCast" },
            ],
        },
        [4] = new SkillConfig
        {
            Id = 4,
            Name = "旋风斩",
            CastRange = 2,
            CastTime = 1.0,
            InterruptOnMove = false,
            PostCastTime = 0.1,
            Cooldown = 8,
            MpCost = 15,
            TargetType = "AllEnemiesInRange",
            Actions =
            [
                new Dictionary<string, object> { ["type"] = "DealDamage", ["damageType"] = "physical", ["coefficient"] = 0.6 },
            ],
        },
    };

    internal static SkillConfig? GetSkillConfig(int skillId)
        => SkillConfigs.GetValueOrDefault(skillId);

    public static string? GetSkillName(int skillId)
        => SkillConfigs.GetValueOrDefault(skillId)?.Name;

    // ---- 阶段 1: Pre-Check ----
    public (bool ok, string? err) PreCheck(int skillId, CombatContext ctx)
    {
        var cfg = GetSkillConfig(skillId);
        if (cfg == null) return (false, "skill_not_found");

        if (ctx.SkillCooldowns.TryGetValue(skillId, out var cdEnd) && Environment.TickCount64 < cdEnd)
            return (false, "cooldown");

        // TODO: MP 检查、状态检查（沉默/眩晕）
        return (true, null);
    }

    // ---- 阶段 2: Target Selection ----
    public List<long>? SelectTargets(int skillId, long casterId, CombatContext ctx, Dictionary<string, MapState>? maps)
    {
        var cfg = GetSkillConfig(skillId);
        if (cfg == null || maps == null) return null;

        var casterPositions = FindEntityCombatPositions(casterId, maps);
        if (casterPositions.Count == 0) return null;

        if (cfg.TargetType == "Self")
            return [casterId];

        if (cfg.TargetType == "SingleEnemy")
        {
            var candidates = new List<(long id, int dist)>();
            foreach (var relationId in ctx.RelationIds)
            {
                var rel = CombatManager!.RelationsMgr.Relations.GetValueOrDefault(relationId);
                if (rel == null || !rel.IsActive || rel.AttackerId != casterId) continue;

                var targetPositions = FindEntityCombatPositions(rel.TargetId, maps);
                if (targetPositions.Count == 0) continue;

                int d = MinCombatDistance(casterPositions, targetPositions);
                if (d <= cfg.CastRange)
                    candidates.Add((rel.TargetId, d));
            }

            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            return candidates.Count > 0 ? [candidates[0].id] : null;
        }

        if (cfg.TargetType == "AllEnemiesInRange")
        {
            var targets = new List<long>();
            foreach (var relationId in ctx.RelationIds)
            {
                var rel = CombatManager!.RelationsMgr.Relations.GetValueOrDefault(relationId);
                if (rel == null || !rel.IsActive || rel.AttackerId != casterId) continue;

                var targetPositions = FindEntityCombatPositions(rel.TargetId, maps);
                if (targetPositions.Count == 0) continue;

                int d = MinCombatDistance(casterPositions, targetPositions);
                if (d <= cfg.CastRange)
                    targets.Add(rel.TargetId);
            }
            return targets.Count > 0 ? targets : null;
        }

        return null;
    }

    // ---- 阶段 3: Cast Start ----
    public void StartCast(long casterId, int skillId)
    {
        var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(casterId);
        var cfg = GetSkillConfig(skillId);
        if (ctx == null || cfg == null) return;

        ctx.SubState = "CASTING";
        ctx.CastSkillId = skillId;
        ctx.CastEndTime = Environment.TickCount64 + (long)(cfg.CastTime * 1000);
    }

    // ---- 阶段 4: Final Validation ----
    public bool FinalValidation(int skillId, long casterId, List<long> targets, Dictionary<string, MapState>? maps)
    {
        var cfg = GetSkillConfig(skillId);
        if (cfg == null || maps == null) return false;

        var casterPositions = FindEntityCombatPositions(casterId, maps);
        if (casterPositions.Count == 0) return false;

        var validTargets = new List<long>();
        foreach (var targetId in targets)
        {
            var targetPositions = FindEntityCombatPositions(targetId, maps);
            if (targetPositions.Count == 0) continue;
            int d = MinCombatDistance(casterPositions, targetPositions);
            if (d <= cfg.CastRange)
                validTargets.Add(targetId);
        }

        if (validTargets.Count == 0)
        {
            var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(casterId);
            if (ctx != null)
            {
                var fallback = SelectTargets(skillId, casterId, ctx, maps);
                if (fallback != null && fallback.Count > 0)
                {
                    targets.Clear();
                    targets.AddRange(fallback);
                    return true;
                }
            }
        }

        return validTargets.Count > 0;
    }

    // ---- 阶段 5: Execute Actions ----
    public void ExecuteActions(int skillId, long casterId, List<long> targets, Dictionary<string, MapState>? maps)
    {
        var cfg = GetSkillConfig(skillId);
        if (cfg == null) return;

        var context = new ActionContext
        {
            SkillId = skillId,
            SkillLevel = 1,
            CombatManager = CombatManager,
            Maps = maps,
        };

        foreach (var actionCfg in cfg.Actions)
        {
            string? actionType = actionCfg.TryGetValue("type", out var t) ? t as string : null;
            if (actionType == null) continue;

            var handler = _actionRegistry.Get(actionType);
            if (handler != null)
            {
                context.ActionParams = actionCfg;
                try
                {
                    handler.Execute(casterId, targets, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SkillPipeline] action error: {ActionType}", actionType);
                }
            }
            else
            {
                _logger.LogWarning("[SkillPipeline] unknown action: {ActionType}", actionType);
            }
        }
    }

    // ---- 阶段 6: Cast End ----
    public void EndCast(long casterId, int skillId, bool isMiss)
    {
        var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(casterId);
        var cfg = GetSkillConfig(skillId);
        if (ctx == null || cfg == null) return;

        ctx.SkillCooldowns[skillId] = Environment.TickCount64 + (long)(cfg.Cooldown * 1000);
        ctx.SubState = "POST_CAST";
        ctx.CastSkillId = null;
        ctx.CastEndTime = null;
        ctx.PostCastEndTime = Environment.TickCount64 + (long)(cfg.PostCastTime * 1000);
    }

    public void ClearCast(long casterId)
    {
        var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(casterId);
        if (ctx == null) return;
        ctx.SubState = "NONE";
        ctx.CastSkillId = null;
        ctx.CastEndTime = null;
    }

    // ---- 主接口 ----
    public string Cast(int skillId, long casterId, Dictionary<string, MapState>? maps)
    {
        var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(casterId);
        if (ctx == null) return "FAILURE";

        // 阶段 1: Pre-Check
        var (ok, err) = PreCheck(skillId, ctx);
        if (!ok) return "FAILURE";

        // 阶段 2: Target Selection
        var targets = SelectTargets(skillId, casterId, ctx, maps);
        if (targets == null || targets.Count == 0) return "FAILURE";

        // 阶段 3: Cast Start
        var cfg = GetSkillConfig(skillId);
        double castTime = cfg?.CastTime ?? 0;
        StartCast(casterId, skillId);

        if (castTime > 0) return "PENDING";

        // 阶段 4: Final Validation
        if (!FinalValidation(skillId, casterId, targets, maps))
        {
            EndCast(casterId, skillId, true);
            return "MISS";
        }

        // 阶段 5: Execute Actions
        ExecuteActions(skillId, casterId, targets, maps);

        // 阶段 6: Cast End
        EndCast(casterId, skillId, false);
        return "SUCCESS";
    }

    // 读条续接
    public string ResumeCast(long casterId, Dictionary<string, MapState>? maps)
    {
        var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(casterId);
        if (ctx == null || ctx.SubState != "CASTING") return "FAILURE";

        int? skillId = ctx.CastSkillId;
        if (skillId == null)
        {
            ClearCast(casterId);
            return "FAILURE";
        }

        if (Environment.TickCount64 < ctx.CastEndTime) return "PENDING";

        var targets = SelectTargets(skillId.Value, casterId, ctx, maps);
        if (targets == null || targets.Count == 0)
        {
            EndCast(casterId, skillId.Value, true);
            return "MISS";
        }

        if (!FinalValidation(skillId.Value, casterId, targets, maps))
        {
            EndCast(casterId, skillId.Value, true);
            return "MISS";
        }

        ExecuteActions(skillId.Value, casterId, targets, maps);
        EndCast(casterId, skillId.Value, false);
        return "SUCCESS";
    }

    // ---- Helpers ----

    public static (string? mapName, (int x, int y)? pos) FindEntityPosition(long entityId, Dictionary<string, MapState> maps)
    {
        foreach (var (mapName, map) in maps)
        {
            if (map.Players.TryGetValue(entityId, out var p))
                return (mapName, (p.GridX, p.GridY));
            if (map.Monsters.TryGetValue(entityId, out var m))
                return (mapName, (m.X, m.Y));
        }
        return (null, null);
    }

    /// <summary>
    /// 获取实体的所有战斗位置（支持双格区间）
    /// </summary>
    public static List<(string mapName, int x, int y)> FindEntityCombatPositions(long entityId, Dictionary<string, MapState> maps)
    {
        foreach (var (mapName, map) in maps)
        {
            if (map.Players.TryGetValue(entityId, out var p))
            {
                if (p.CombatPositions.Count > 0)
                    return p.CombatPositions.Select(cp => (mapName, cp.x, cp.y)).ToList();
                return new List<(string, int, int)> { (mapName, p.GridX, p.GridY) };
            }
            if (map.Monsters.TryGetValue(entityId, out var m))
            {
                if (m.CombatPositions.Count > 0)
                    return m.CombatPositions.Select(cp => (mapName, cp.x, cp.y)).ToList();
                return new List<(string, int, int)> { (mapName, m.X, m.Y) };
            }
        }
        return new List<(string, int, int)>();
    }

    /// <summary>
    /// 两组位置之间的最短曼哈顿距离（跨地图返回 MaxValue）
    /// </summary>
    internal static int MinCombatDistance(List<(string mapName, int x, int y)> a, List<(string mapName, int x, int y)> b)
    {
        int minDist = int.MaxValue;
        foreach (var pa in a)
        {
            foreach (var pb in b)
            {
                if (pa.mapName != pb.mapName) continue;
                int d = Math.Abs(pa.x - pb.x) + Math.Abs(pa.y - pb.y);
                if (d < minDist) minDist = d;
            }
        }
        return minDist;
    }

    public static string GetEntityName(long entityId, Dictionary<string, MapState> maps)
    {
        foreach (var map in maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
                return p.RoleName ?? $"player_{entityId}";
            if (map.Monsters.TryGetValue(entityId, out var m))
                return !string.IsNullOrEmpty(m.Name) ? m.Name : $"monster_{entityId}";
        }
        return $"entity_{entityId}";
    }

    public static string? GetEntityMapName(long entityId, Dictionary<string, MapState> maps)
    {
        foreach (var (mapName, map) in maps)
        {
            if (map.Players.ContainsKey(entityId)) return mapName;
            if (map.Monsters.ContainsKey(entityId)) return mapName;
        }
        return null;
    }
}

// ---- Data types ----

public class SkillConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CastRange { get; set; }
    public double CastTime { get; set; }
    public bool InterruptOnMove { get; set; }
    public double PostCastTime { get; set; }
    public double Cooldown { get; set; }
    public int MpCost { get; set; }
    public string TargetType { get; set; } = "SingleEnemy";
    public List<Dictionary<string, object>> Actions { get; set; } = new();
}
