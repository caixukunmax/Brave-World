using GameServer.Services.Core;
using GameServer.Services.Map.Combat.Actions;
using GameServer.Tables;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 技能管线：6 阶段技能释放流程 — 移植自 combat/pipeline.lua
/// </summary>
public class SkillPipeline
{
    private readonly ILogger _logger;
    private readonly ActionRegistry _actionRegistry;
    private readonly LubanTableLoader? _tables;
    public CombatManager? CombatManager { get; set; }
    public ProjectileManager? ProjectileManager { get; set; }

    // 静态引用供无状态 handler 查询技能配置
    private static LubanTableLoader? _staticTables;

    public SkillPipeline(ILogger<SkillPipeline> logger, ActionRegistry actionRegistry, LubanTableLoader? tables = null)
    {
        _logger = logger;
        _actionRegistry = actionRegistry;
        _tables = tables;
        _staticTables = tables;
    }

    // ---- 技能配置 ----

    private SkillConfigRow? GetSkillConfig(int skillId)
    {
        if (_tables != null)
            return _tables.GetSkill(skillId);
        return null;
    }

    /// <summary>静态查询 — 供无状态 handler 使用</summary>
    public static SkillConfigRow? GetSkillConfigStatic(int skillId)
        => _staticTables?.GetSkill(skillId);

    public string? GetSkillName(int skillId)
        => GetSkillConfig(skillId)?.Name;

    public static string? GetSkillNameStatic(int skillId)
        => _staticTables?.GetSkill(skillId)?.Name;

    // ---- 阶段 1: Pre-Check ----
    public (bool ok, string? err) PreCheck(int skillId, CombatContext ctx, long casterId, Dictionary<string, MapState>? maps)
    {
        var cfg = GetSkillConfig(skillId);
        if (cfg == null) return (false, "skill_not_found");

        if (ctx.SkillCooldowns.TryGetValue(skillId, out var cdEnd) && Environment.TickCount64 < cdEnd)
            return (false, "cooldown");

        // MP 检查（怪物跳过，mp_cost=0 的技能也跳过）
        if (cfg.MpCost > 0 && maps != null)
        {
            var casterState = FindPlayerState(casterId, maps);
            if (casterState == null) return (true, null); // 怪物无 MP，跳过检查
            if (casterState.Mp < cfg.MpCost)
                return (false, "insufficient_mp");
        }

        return (true, null);
    }

    // ---- 阶段 2: Target Selection ----
    /// <summary>
    /// 目标选择。
    /// 手动施法默认保留射程过滤；自动战斗通过 ignoreRange=true 跳过射程过滤，
    /// 让 Final Validation 阶段按射程判定是否 Miss。
    /// </summary>
    public List<long>? SelectTargets(int skillId, long casterId, CombatContext ctx, Dictionary<string, MapState>? maps, bool ignoreRange = false)
    {
        var cfg = GetSkillConfig(skillId);
        if (cfg == null || maps == null) return null;

        var casterPositions = FindEntityCombatPositions(casterId, maps);
        if (casterPositions.Count == 0) return null;

        if (cfg.TargetType == ESkillTargetType.Self)
            return [casterId];

        if (cfg.TargetType == ESkillTargetType.SingleEnemy)
        {
            var candidates = new List<(long id, int dist)>();
            foreach (var relationId in ctx.RelationIds)
            {
                var rel = CombatManager!.RelationsMgr.Relations.GetValueOrDefault(relationId);
                if (rel == null || !rel.IsActive || rel.AttackerId != casterId) continue;

                var targetPositions = FindEntityCombatPositions(rel.TargetId, maps);
                if (targetPositions.Count == 0) continue;

                int d = MinCombatDistance(casterPositions, targetPositions);
                if (ignoreRange || d <= cfg.CastRange)
                    candidates.Add((rel.TargetId, d));
            }

            // 优先攻击目标：手动施法或目标在射程内时优先使用；自动战斗且目标在射程外时进入候选排序
            if (ctx.PriorityTargetId > 0)
            {
                var priorityPositions = FindEntityCombatPositions(ctx.PriorityTargetId, maps);
                if (priorityPositions.Count > 0)
                {
                    int pd = MinCombatDistance(casterPositions, priorityPositions);
                    if (!ignoreRange && pd <= cfg.CastRange)
                        return [ctx.PriorityTargetId];
                    if (ignoreRange)
                        candidates.Add((ctx.PriorityTargetId, pd));
                }
            }

            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            return candidates.Count > 0 ? [candidates[0].id] : null;
        }

        if (cfg.TargetType == ESkillTargetType.AllEnemiesInRange)
        {
            var targets = new List<long>();
            foreach (var relationId in ctx.RelationIds)
            {
                var rel = CombatManager!.RelationsMgr.Relations.GetValueOrDefault(relationId);
                if (rel == null || !rel.IsActive || rel.AttackerId != casterId) continue;

                var targetPositions = FindEntityCombatPositions(rel.TargetId, maps);
                if (targetPositions.Count == 0) continue;

                int d = MinCombatDistance(casterPositions, targetPositions);
                if (ignoreRange || d <= cfg.CastRange)
                    targets.Add(rel.TargetId);
            }
            return targets.Count > 0 ? targets : null;
        }

        if (cfg.TargetType == ESkillTargetType.AllAlliesInRange)
        {
            var allies = new List<long>();
            bool casterIsPlayer = casterId < 1000000;

            foreach (var (mapName, map) in maps)
            {
                // 检查施法者是否在这张地图
                bool casterOnThisMap = casterIsPlayer
                    ? map.Players.ContainsKey(casterId)
                    : map.Monsters.ContainsKey(casterId);
                if (!casterOnThisMap) continue;

                if (casterIsPlayer)
                {
                    foreach (var (allyId, _) in map.Players)
                    {
                        var allyPositions = FindEntityCombatPositions(allyId, maps);
                        if (allyPositions.Count == 0) continue;
                        int d = MinCombatDistance(casterPositions, allyPositions);
                        if (ignoreRange || d <= cfg.CastRange)
                            allies.Add(allyId);
                    }
                }
                else
                {
                    foreach (var (allyId, _) in map.Monsters)
                    {
                        var allyPositions = FindEntityCombatPositions(allyId, maps);
                        if (allyPositions.Count == 0) continue;
                        int d = MinCombatDistance(casterPositions, allyPositions);
                        if (ignoreRange || d <= cfg.CastRange)
                            allies.Add(allyId);
                    }
                }
                break; // 施法者只会在一张地图上
            }
            return allies.Count > 0 ? allies : null;
        }

        return null;
    }

    // ---- 阶段 3: Cast Start ----
    public void StartCast(long casterId, int skillId, Dictionary<string, MapState>? maps, double? actualCastTime = null)
    {
        var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(casterId);
        var cfg = GetSkillConfig(skillId);
        if (ctx == null || cfg == null) return;

        ctx.SubState = "CASTING";
        ctx.CastSkillId = skillId;
        double effectiveCastTime = actualCastTime ?? cfg.CastTime;
        ctx.CastEndTime = Environment.TickCount64 + (long)(effectiveCastTime * 1000);

        // 扣除 MP（仅玩家，怪物 mp_cost=0 不扣）
        if (cfg.MpCost > 0 && maps != null)
        {
            var casterState = FindPlayerState(casterId, maps);
            if (casterState != null)
                casterState.Mp -= cfg.MpCost;
        }
    }

    public void InterruptCast(long entityId, Dictionary<string, MapState>? maps = null)
    {
        var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(entityId);
        if (ctx == null) return;

        if (ctx.SubState == "CASTING" || ctx.SubState == "POST_CAST")
        {
            // 读条阶段中断：返还已扣除的 MP（后摇阶段技能已生效，不返还）
            if (ctx.SubState == "CASTING" && ctx.CastSkillId.HasValue && maps != null)
            {
                var cfg = GetSkillConfig(ctx.CastSkillId.Value);
                if (cfg != null && cfg.MpCost > 0)
                    RefundMp(entityId, cfg.MpCost, maps);
            }

            ctx.SubState = "NONE";
            ctx.CastSkillId = null;
            ctx.CastEndTime = null;
            ctx.PostCastEndTime = null;
        }
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

        // Self 目标类型：自身距离为 0，总是合法
        if (cfg.TargetType == ESkillTargetType.Self && targets.Contains(casterId))
            return true;

        // AllAlliesInRange: 将合法友方写回 targets 列表
        if (cfg.TargetType == ESkillTargetType.AllAlliesInRange && validTargets.Count > 0)
        {
            targets.Clear();
            targets.AddRange(validTargets);
            return true;
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
            ProjectileManager = ProjectileManager,
        };

        foreach (var actionCfg in cfg.Actions)
        {
            // 将 CombatActionBeanRow 转为 ICombatAction 所需的字典格式
            string actionType = actionCfg.ActionType switch
            {
                1 => "DealDamage",    // ECombatActionType.DealDamage
                2 => "InterruptCast", // ECombatActionType.InterruptCast
                3 => "Heal",          // ECombatActionType.Heal
                4 => "ApplyBuff",     // ECombatActionType.ApplyBuff
                5 => "ApplyBuff",     // ECombatActionType.ApplyShield (复用 ApplyBuff，shield_base 在 BuffConfig 里)
                6 => "Purify",        // ECombatActionType.Purify
                7 => "SpawnProjectile", // ECombatActionType.SpawnProjectile
                _ => null!
            };
            if (actionType == null) continue;

            // SpawnProjectile 特殊处理：直接生成弹道，不走 ActionRegistry
            if (actionType == "SpawnProjectile")
            {
                SpawnProjectile(casterId, targets, context, cfg, actionCfg);
                continue;
            }

            var handler = _actionRegistry.Get(actionType);
            if (handler != null)
            {
                var paramDict = new Dictionary<string, object>
                {
                    ["type"] = actionType,
                    ["damageType"] = actionCfg.DamageType == 2 ? "magical" : "physical",
                    ["coefficient"] = actionCfg.Coefficient,
                    ["buff_id"] = actionCfg.BuffId,
                };
                context.ActionParams = paramDict;
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
    /// <summary>手动施法入口：目标选择受射程过滤约束。</summary>
    public virtual string Cast(int skillId, long casterId, Dictionary<string, MapState>? maps)
        => CastCore(skillId, casterId, maps, ignoreRange: false);

    /// <summary>自动战斗/AI 施法入口：目标选择忽略射程，由 Final Validation 判定 Miss。</summary>
    public virtual string CastForAuto(int skillId, long casterId, Dictionary<string, MapState>? maps)
        => CastCore(skillId, casterId, maps, ignoreRange: true);

    private string CastCore(int skillId, long casterId, Dictionary<string, MapState>? maps, bool ignoreRange)
    {
        var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(casterId);
        if (ctx == null) return "FAILURE";

        // 阶段 1: Pre-Check
        var (ok, err) = PreCheck(skillId, ctx, casterId, maps);
        var combatId = CombatManager?.GetCombatId(casterId) ?? 0;
        CombatTrace.SkillPreCheck(_logger, combatId, casterId, SkillPipeline.GetEntityName(casterId, maps!), skillId, ok, err);
        if (!ok) return "FAILURE";

        // 阶段 2: Target Selection
        var targets = SelectTargets(skillId, casterId, ctx, maps, ignoreRange);
        if (targets == null || targets.Count == 0) return "FAILURE";
        var cfg = GetSkillConfig(skillId);
        CombatTrace.SkillSelectTargets(_logger, combatId, casterId, SkillPipeline.GetEntityName(casterId, maps!), skillId, cfg?.TargetType.ToString() ?? "", targets);

        // 阶段 3: Cast Start
        double castTime = cfg?.CastTime ?? 0;

        // 应用先攻加速：首个技能且未使用过先手时，按百分比减少 CastTime
        if (!ctx.HasUsedFirstStrike && ctx.FirstStrikeHaste > 0 && castTime > 0)
        {
            double hasteRatio = ctx.FirstStrikeHaste / 100.0;
            castTime = castTime * (1.0 - hasteRatio);
            if (castTime < 0) castTime = 0;
            ctx.HasUsedFirstStrike = true;
        }

        CombatTrace.SkillStartCast(_logger, combatId, casterId, SkillPipeline.GetEntityName(casterId, maps!), skillId, castTime, cfg?.MpCost ?? 0);
        StartCast(casterId, skillId, maps, castTime);

        if (castTime > 0 && maps != null && CombatManager != null)
        {
            var primaryTargetId = targets.Count > 0 ? targets[0] : casterId;
            var (_, pos) = SkillPipeline.FindEntityPosition(primaryTargetId, maps);
            CombatManager.BroadcastCastStartNotify(casterId, skillId, (float)castTime, primaryTargetId, pos?.x ?? 0, pos?.y ?? 0, maps);
            return "PENDING";
        }

        // 阶段 4: Final Validation
        if (!FinalValidation(skillId, casterId, targets, maps))
        {
            CombatTrace.SkillFinalValidation(_logger, combatId, casterId, SkillPipeline.GetEntityName(casterId, maps!), skillId, false, targets);
            // Miss 时不返还 MP，进入完整 CD（自动战斗/风筝惩罚）
            if (maps != null && CombatManager != null)
                CombatManager.BroadcastCastResultNotify(casterId, skillId, targets, true, maps);
            EndCast(casterId, skillId, true);
            return "MISS";
        }
        CombatTrace.SkillFinalValidation(_logger, combatId, casterId, SkillPipeline.GetEntityName(casterId, maps!), skillId, true, targets);

        // 阶段 5: Execute Actions
        CombatTrace.SkillExecuteAction(_logger, combatId, casterId, SkillPipeline.GetEntityName(casterId, maps!), skillId, "ExecuteActions", targets);
        ExecuteActions(skillId, casterId, targets, maps);

        // 阶段 6: Cast End
        EndCast(casterId, skillId, false);
        if (maps != null && CombatManager != null)
            CombatManager.BroadcastCastResultNotify(casterId, skillId, targets, false, maps);
        CombatTrace.SkillCastResult(_logger, combatId, casterId, SkillPipeline.GetEntityName(casterId, maps!), skillId, "SUCCESS");
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

        // 读条期间目标死亡/消失：尝试自动切换为仇恨最高的敌人
        if (targets == null || targets.Count == 0)
        {
            var fallbackTargets = SelectTargetsByHate(skillId.Value, casterId, ctx, maps);
            if (fallbackTargets != null && fallbackTargets.Count > 0)
            {
                targets = fallbackTargets;
            }
        }

        // 仍然找不到目标：中断读条，返还 MP
        if (targets == null || targets.Count == 0)
        {
            var cfg = GetSkillConfig(skillId.Value);
            if (cfg != null && cfg.MpCost > 0 && maps != null)
            {
                RefundMp(casterId, cfg.MpCost, maps);
            }
            if (maps != null && CombatManager != null)
                CombatManager.BroadcastCastResultNotify(casterId, skillId.Value, new List<long>(), true, maps);
            EndCast(casterId, skillId.Value, true);
            return "INTERRUPTED";
        }

        if (!FinalValidation(skillId.Value, casterId, targets, maps))
        {
            // Miss：MP 不返还，技能进入完整 CD
            if (maps != null && CombatManager != null)
                CombatManager.BroadcastCastResultNotify(casterId, skillId.Value, targets, true, maps);
            EndCast(casterId, skillId.Value, true);
            return "MISS";
        }

        ExecuteActions(skillId.Value, casterId, targets, maps);
        EndCast(casterId, skillId.Value, false);
        if (maps != null && CombatManager != null)
            CombatManager.BroadcastCastResultNotify(casterId, skillId.Value, targets, false, maps);
        CombatTrace.SkillCastResult(_logger, CombatManager?.GetCombatId(casterId) ?? 0, casterId, SkillPipeline.GetEntityName(casterId, maps!), skillId.Value, "SUCCESS");
        return "SUCCESS";
    }

    // ---- SpawnProjectile ----
    private void SpawnProjectile(long casterId, List<long> targets, ActionContext context, SkillConfigRow cfg, CombatActionBeanRow actionCfg)
    {
        var maps = context.Maps;
        var projectileMgr = context.ProjectileManager;
        var combatMgr = context.CombatManager;
        if (maps == null || projectileMgr == null || combatMgr == null)
        {
            _logger.LogWarning("[SkillPipeline] SpawnProjectile missing dependencies");
            return;
        }

        // 获取施法者位置
        var (casterMap, casterPos) = FindEntityPosition(casterId, maps);
        if (casterMap == null || casterPos == null)
        {
            _logger.LogWarning("[SkillPipeline] SpawnProjectile caster position not found");
            return;
        }

        // 获取目标位置（取 targets[0] 的当前位置）
        int targetX = casterPos.Value.x;
        int targetY = casterPos.Value.y;
        if (targets.Count > 0)
        {
            var (tMap, tPos) = FindEntityPosition(targets[0], maps);
            if (tMap == casterMap && tPos != null)
            {
                targetX = tPos.Value.x;
                targetY = tPos.Value.y;
            }
            else
            {
                _logger.LogWarning("[SkillPipeline] SpawnProjectile target position not found, skipping");
                return;
            }
        }

        string damageType = actionCfg.DamageType == 2 ? "magical" : "physical";
        float speed = cfg.ProjectileSpeed > 0 ? cfg.ProjectileSpeed : 5f;
        int maxRange = cfg.ProjectileMaxRange > 0 ? cfg.ProjectileMaxRange : cfg.CastRange;

        long projectileId = projectileMgr.Spawn(
            casterId, cfg.Id, casterMap,
            casterPos.Value.x, casterPos.Value.y,
            targetX, targetY,
            speed, maxRange, damageType, actionCfg.Coefficient);

        // 广播弹道生成通知
        combatMgr.BroadcastProjectileSpawnNotify(projectileId, casterId, cfg.Id,
            casterPos.Value.x, casterPos.Value.y, targetX, targetY, speed, maps);
    }

    // ---- Helpers ----

    private static MapPlayerState? FindPlayerState(long entityId, Dictionary<string, MapState> maps)
    {
        foreach (var map in maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
                return p;
        }
        return null;
    }

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
            if (map.Npcs.TryGetValue(entityId, out var n))
                return !string.IsNullOrEmpty(n.Name) ? n.Name : $"npc_{entityId}";
        }
        return $"entity_{entityId}";
    }

    public static string? GetEntityMapName(long entityId, Dictionary<string, MapState> maps)
    {
        foreach (var (mapName, map) in maps)
        {
            if (map.Players.ContainsKey(entityId)) return mapName;
            if (map.Monsters.ContainsKey(entityId)) return mapName;
            if (map.Npcs.ContainsKey(entityId)) return mapName;
        }
        return null;
    }

    /// <summary>
    /// 按仇恨优先级选择目标（读条期间原目标死亡时的自动切换）。
    /// 优先级：挑衅期内 > 距离最近 > 血量最低 > 累计伤害最高。
    /// </summary>
    public List<long>? SelectTargetsByHate(int skillId, long casterId, CombatContext ctx, Dictionary<string, MapState>? maps)
    {
        var cfg = GetSkillConfig(skillId);
        if (cfg == null || maps == null) return null;

        var casterPositions = FindEntityCombatPositions(casterId, maps);
        if (casterPositions.Count == 0) return null;

        if (cfg.TargetType == ESkillTargetType.SingleEnemy)
        {
            long nowMs = Environment.TickCount64;
            var candidates = new List<(long id, int dist, int hpPercent, int accumulatedDmg, bool inTaunt)>();

            foreach (var relationId in ctx.RelationIds)
            {
                var rel = CombatManager!.RelationsMgr.Relations.GetValueOrDefault(relationId);
                if (rel == null || !rel.IsActive || rel.AttackerId != casterId) continue;

                var targetPositions = FindEntityCombatPositions(rel.TargetId, maps);
                if (targetPositions.Count == 0) continue;

                int d = MinCombatDistance(casterPositions, targetPositions);
                if (d > cfg.CastRange) continue;

                // 获取目标血量百分比
                int hpPercent = 100;
                int maxHp = 1;
                foreach (var map in maps.Values)
                {
                    if (map.Players.TryGetValue(rel.TargetId, out var p))
                    { maxHp = Math.Max(1, p.MaxHp); hpPercent = p.Hp * 100 / maxHp; break; }
                    if (map.Monsters.TryGetValue(rel.TargetId, out var m))
                    { maxHp = Math.Max(1, m.MaxHp); hpPercent = m.Hp * 100 / maxHp; break; }
                }

                bool inTaunt = rel.TauntEndTime > nowMs && rel.TauntSourceId == casterId;
                candidates.Add((rel.TargetId, d, hpPercent, rel.AccumulatedDamage, inTaunt));
            }

            if (candidates.Count == 0) return null;

            // 排序：挑衅期内 > 距离最近 > 血量最低 > 累计伤害最高
            candidates.Sort((a, b) =>
            {
                int cmp = b.inTaunt.CompareTo(a.inTaunt); // true > false
                if (cmp != 0) return cmp;
                cmp = a.dist.CompareTo(b.dist);
                if (cmp != 0) return cmp;
                cmp = a.hpPercent.CompareTo(b.hpPercent);
                if (cmp != 0) return cmp;
                return b.accumulatedDmg.CompareTo(a.accumulatedDmg);
            });

            return [candidates[0].id];
        }

        // 其他目标类型暂时按普通SelectTargets处理
        return SelectTargets(skillId, casterId, ctx, maps);
    }

    /// <summary>
    /// 返还 MP 给施法者（读条中断时）。
    /// </summary>
    private static void RefundMp(long entityId, int mpCost, Dictionary<string, MapState> maps)
    {
        foreach (var map in maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
            {
                p.Mp = Math.Min(p.MaxMp, p.Mp + mpCost);
                return;
            }
            if (map.Monsters.TryGetValue(entityId, out var m))
            {
                m.Mp = Math.Min(m.MaxMp, m.Mp + mpCost);
                return;
            }
        }
    }
}
