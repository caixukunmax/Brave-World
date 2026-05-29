using GameServer.Common.Buffs;
using GameServer.Tables;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 战斗关系管理 — 纯数据 CRUD，无网络依赖
/// </summary>
public class CombatRelationManager
{
    public readonly Dictionary<int, CombatRelation> Relations = new();
    public readonly Dictionary<long, CombatContext> Contexts = new();
    private int _nextRelationId = 1;
    private readonly LubanTableLoader? _tables;

    public CombatRelationManager(LubanTableLoader? tables = null)
    {
        _tables = tables;
    }

    public CombatContext GetOrCreateContext(long entityId, BuffContainer? sharedBuffs = null)
    {
        if (!Contexts.TryGetValue(entityId, out var ctx))
        {
            ctx = new CombatContext { EntityId = entityId, Buffs = sharedBuffs ?? new BuffContainer(_tables) };
            Contexts[entityId] = ctx;
        }
        return ctx;
    }

    public void SetState(long entityId, string state)
    {
        var ctx = Contexts.GetValueOrDefault(entityId);
        if (ctx == null) return;
        ctx.State = state;
        if (state == "IDLE")
        {
            ctx.SubState = "NONE";
            ctx.CastSkillId = null;
            ctx.CastEndTime = null;
            ctx.PostCastEndTime = null;
            ctx.FirstStrikeHaste = 0;
            ctx.HasUsedFirstStrike = false;
            ctx.PriorityTargetId = 0;
        }
    }

    public int CreateRelation(long attackerId, long targetId)
    {
        foreach (var (_, existing) in Relations)
        {
            if (existing.AttackerId == attackerId && existing.TargetId == targetId && existing.IsActive)
                return existing.RelationId;
        }

        int relationId = _nextRelationId++;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long nowMs = Environment.TickCount64;
        Relations[relationId] = new CombatRelation
        {
            RelationId = relationId,
            AttackerId = attackerId,
            TargetId = targetId,
            StartTime = now,
            LastDamageTime = now,
            IsActive = true,
            TauntEndTime = nowMs + 2000, // 挑衅期 2 秒
            TauntSourceId = attackerId,
        };

        GetOrCreateContext(attackerId).RelationIds.Add(relationId);
        GetOrCreateContext(targetId).RelationIds.Add(relationId);
        return relationId;
    }

    /// <summary>
    /// 刷新挑衅期：将 targetId 作为被挑衅者，sourceId 作为挑衅者，刷新/设置双向关系的挑衅期。
    /// </summary>
    public void RefreshTaunt(long sourceId, long targetId)
    {
        long nowMs = Environment.TickCount64;
        long tauntEnd = nowMs + 2000;

        foreach (var (_, rel) in Relations)
        {
            if (!rel.IsActive) continue;
            // 更新 sourceId → targetId 的关系
            if (rel.AttackerId == sourceId && rel.TargetId == targetId)
            {
                rel.TauntEndTime = tauntEnd;
                rel.TauntSourceId = sourceId;
            }
            // 更新 targetId → sourceId 的关系（双向同步）
            if (rel.AttackerId == targetId && rel.TargetId == sourceId)
            {
                rel.TauntEndTime = tauntEnd;
                rel.TauntSourceId = sourceId;
            }
        }
    }

    public void RemoveRelation(int relationId)
    {
        var rel = Relations.GetValueOrDefault(relationId);
        if (rel == null || !rel.IsActive) return;
        rel.IsActive = false;

        var ctxA = Contexts.GetValueOrDefault(rel.AttackerId);
        var ctxB = Contexts.GetValueOrDefault(rel.TargetId);
        ctxA?.RelationIds.Remove(relationId);
        ctxB?.RelationIds.Remove(relationId);

        if (ctxA != null && ctxA.RelationIds.Count == 0) SetState(rel.AttackerId, "IDLE");
        if (ctxB != null && ctxB.RelationIds.Count == 0) SetState(rel.TargetId, "IDLE");
    }

    /// <summary>
    /// 双向移除：断开 entityA 与 entityB 之间的所有活跃关系。
    /// </summary>
    public void RemoveBidirectionalRelation(long entityA, long entityB)
    {
        var toRemove = new List<int>();
        foreach (var (relationId, rel) in Relations)
        {
            if (!rel.IsActive) continue;
            if ((rel.AttackerId == entityA && rel.TargetId == entityB) ||
                (rel.AttackerId == entityB && rel.TargetId == entityA))
            {
                toRemove.Add(relationId);
            }
        }
        foreach (var relationId in toRemove)
            RemoveRelation(relationId);
    }

    public void OnEntityRemoved(long entityId)
    {
        var ctx = Contexts.GetValueOrDefault(entityId);
        if (ctx == null) return;
        foreach (var relationId in ctx.RelationIds.ToList())
            RemoveRelation(relationId);
        Contexts.Remove(entityId);
    }

    public bool HasActiveRelation(long entityA, long entityB)
    {
        foreach (var (_, rel) in Relations)
        {
            if (rel.IsActive &&
                ((rel.AttackerId == entityA && rel.TargetId == entityB) ||
                 (rel.AttackerId == entityB && rel.TargetId == entityA)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 更新双方所有活跃关系的 LastDamageTime。
    /// </summary>
    public void UpdateLastDamageTime(long attackerId, long targetId)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var (_, rel) in Relations)
        {
            if (!rel.IsActive) continue;
            if ((rel.AttackerId == attackerId && rel.TargetId == targetId) ||
                (rel.AttackerId == targetId && rel.TargetId == attackerId))
            {
                rel.LastDamageTime = now;
            }
        }
    }

    /// <summary>
    /// 累加指定关系方向的伤害。
    /// </summary>
    public void AddAccumulatedDamage(long attackerId, long targetId, int damage)
    {
        foreach (var (_, rel) in Relations)
        {
            if (!rel.IsActive) continue;
            if (rel.AttackerId == attackerId && rel.TargetId == targetId)
            {
                rel.AccumulatedDamage += damage;
            }
        }
    }

    /// <summary>
    /// 获取指定实体当前的所有活跃敌人ID列表。
    /// </summary>
    public List<long> GetActiveEnemies(long entityId)
    {
        var enemies = new HashSet<long>();
        var ctx = Contexts.GetValueOrDefault(entityId);
        if (ctx == null) return enemies.ToList();

        foreach (var relationId in ctx.RelationIds)
        {
            if (!Relations.TryGetValue(relationId, out var rel)) continue;
            if (!rel.IsActive) continue;
            // 关系的另一方就是敌人
            long enemyId = rel.AttackerId == entityId ? rel.TargetId : rel.AttackerId;
            enemies.Add(enemyId);
        }
        return enemies.ToList();
    }
}
