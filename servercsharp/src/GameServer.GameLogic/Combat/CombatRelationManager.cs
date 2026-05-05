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
            ctx.AtbValue = 0;
            ctx.CastSkillId = null;
            ctx.CastEndTime = null;
            ctx.PostCastEndTime = null;
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
        Relations[relationId] = new CombatRelation
        {
            RelationId = relationId,
            AttackerId = attackerId,
            TargetId = targetId,
            StartTime = now,
            LastDamageTime = now,
            IsActive = true,
        };

        GetOrCreateContext(attackerId).RelationIds.Add(relationId);
        GetOrCreateContext(targetId).RelationIds.Add(relationId);
        return relationId;
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

    public void UpdateLastDamageTime(long attackerId, long targetId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var (_, rel) in Relations)
        {
            if (rel.IsActive &&
                ((rel.AttackerId == attackerId && rel.TargetId == targetId) ||
                 (rel.AttackerId == targetId && rel.TargetId == attackerId)))
            {
                rel.LastDamageTime = now;
            }
        }
    }
}
