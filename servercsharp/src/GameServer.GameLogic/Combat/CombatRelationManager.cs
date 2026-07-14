using System.Collections.Concurrent;
using GameServer.Common.Buffs;
using GameServer.Tables;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 战斗关系管理 — 纯数据 CRUD，无网络依赖
/// 使用 ConcurrentDictionary 以支持多线程安全读取；写操作在单线程调度器内执行。
/// </summary>
public class CombatRelationManager
{
    public readonly ConcurrentDictionary<int, CombatRelation> Relations = new();
    public readonly ConcurrentDictionary<long, CombatContext> Contexts = new();
    private int _nextRelationId = 1;
    private readonly LubanTableLoader? _tables;

    /// <summary>
    /// 双向索引：(entityA, entityB) → relationId。
    /// key 的两个 ID 已排序（min, max），不区分方向，查找 O(1)。
    /// </summary>
    private readonly Dictionary<(long min, long max), int> _pairIndex = new();

    private static (long min, long max) MakePair(long a, long b) =>
        a < b ? (a, b) : (b, a);

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
        // O(1) 检查已存在的活跃关系
        var pairKey = MakePair(attackerId, targetId);
        if (_pairIndex.TryGetValue(pairKey, out var existingId) &&
            Relations.TryGetValue(existingId, out var existing) && existing.IsActive)
            return existing.RelationId;

        int relationId = Interlocked.Increment(ref _nextRelationId);
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

        _pairIndex[pairKey] = relationId;
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

        var pairKey = MakePair(sourceId, targetId);
        if (_pairIndex.TryGetValue(pairKey, out var relationId))
        {
            if (Relations.TryGetValue(relationId, out var rel) && rel.IsActive)
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

        _pairIndex.Remove(MakePair(rel.AttackerId, rel.TargetId));

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
        var pairKey = MakePair(entityA, entityB);
        if (_pairIndex.TryGetValue(pairKey, out var relationId))
            RemoveRelation(relationId);
    }

    public void OnEntityRemoved(long entityId)
    {
        var ctx = Contexts.GetValueOrDefault(entityId);
        if (ctx == null) return;
        foreach (var relationId in ctx.RelationIds.ToList())
            RemoveRelation(relationId);
        Contexts.TryRemove(entityId, out _);
    }

    public bool HasActiveRelation(long entityA, long entityB)
    {
        var pairKey = MakePair(entityA, entityB);
        if (!_pairIndex.TryGetValue(pairKey, out var relationId))
            return false;
        if (!Relations.TryGetValue(relationId, out var rel))
            return false;
        return rel.IsActive;
    }

    /// <summary>
    /// 更新双方所有活跃关系的 LastDamageTime。
    /// </summary>
    public void UpdateLastDamageTime(long attackerId, long targetId)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var pairKey = MakePair(attackerId, targetId);
        if (_pairIndex.TryGetValue(pairKey, out var relationId) &&
            Relations.TryGetValue(relationId, out var rel) && rel.IsActive)
        {
            rel.LastDamageTime = now;
        }
    }

    /// <summary>
    /// 累加指定关系方向的伤害。
    /// </summary>
    public void AddAccumulatedDamage(long attackerId, long targetId, int damage)
    {
        var pairKey = MakePair(attackerId, targetId);
        if (_pairIndex.TryGetValue(pairKey, out var relationId))
        {
            if (Relations.TryGetValue(relationId, out var rel) && rel.IsActive)
            {
                if (rel.AttackerId == attackerId && rel.TargetId == targetId)
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
