using GameServer.Services.Core;
using GameServer.Services.Map.Combat.Actions;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PGame = global::Game;
using PProtocol = global::Protocol;
using INetworkSender = GameServer.Services.Core.INetworkSender;

namespace GameServer.Services.Map.Combat;

/// <summary>
/// 战斗系统协调器 — 组合各子系统，替代原 CombatManager 上帝类
/// 依赖接口而非具体实现，无循环依赖
/// </summary>
public class CombatManager
{
    private readonly ILogger<CombatManager> _logger;
    private readonly SkillPipeline _pipeline;
    private readonly INetworkSender _network;
    private readonly CombatRelationManager _relations;
    private readonly DisengageSystem _disengage;

    public CombatRelationManager RelationsMgr => _relations;

    public CombatManager(
        ILogger<CombatManager> logger,
        SkillPipeline pipeline,
        ActionRegistry actionRegistry,
        INetworkSender network)
    {
        _logger = logger;
        _pipeline = pipeline;
        _network = network;
        _relations = new CombatRelationManager();
        _disengage = new DisengageSystem(logger);

        // 绑定 pipeline 反向引用（Phase 3 后续会移除）
        _pipeline.CombatManager = this;
    }

    // ---- 碰撞入口 ----

    public bool IsCasting(long entityId)
    {
        var ctx = _relations.Contexts.GetValueOrDefault(entityId);
        return ctx != null && ctx.SubState == "CASTING";
    }

    public void OnCollision(long entityA, long entityB, Dictionary<string, MapState> maps)
    {
        if (_relations.HasActiveRelation(entityA, entityB))
        {
            _logger.LogDebug("[Combat] onCollision skipped: A={A} B={B} already in combat", entityA, entityB);
            return;
        }

        _logger.LogInformation("[Combat] onCollision: A={A} B={B}", entityA, entityB);

        _relations.CreateRelation(entityA, entityB);
        _relations.CreateRelation(entityB, entityA);

        var ctxA = _relations.GetOrCreateContext(entityA);
        var ctxB = _relations.GetOrCreateContext(entityB);

        SetCombatJob(ctxA, entityA, maps);
        SetCombatJob(ctxB, entityB, maps);

        if (ctxA.State == "IDLE" || ctxB.State == "IDLE")
        {
            ExecuteFirstStrike(entityA, entityB, maps);
            ExecuteFirstStrike(entityB, entityA, maps);
        }

        _relations.SetState(entityA, "COMBAT");
        _relations.SetState(entityB, "COMBAT");

        // 广播战斗开始
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string mapName = SkillPipeline.GetEntityMapName(entityA, maps) ?? SkillPipeline.GetEntityMapName(entityB, maps) ?? "";
        BroadcastCombatLog(new List<CombatLogEntry>
        {
            new()
            {
                LogType = PGame.CombatLogType.CombatLogStart, Timestamp = (ulong)now,
                ActorName = SkillPipeline.GetEntityName(entityA, maps),
                TargetName = SkillPipeline.GetEntityName(entityB, maps),
                Extra = "进入了战斗！", ActorId = entityA, MapName = mapName,
            }
        }, maps);
    }

    public void ExecuteFirstStrike(long attackerId, long targetId, Dictionary<string, MapState> maps)
    {
        var attackerPositions = SkillPipeline.FindEntityCombatPositions(attackerId, maps);
        var targetPositions = SkillPipeline.FindEntityCombatPositions(targetId, maps);
        if (attackerPositions.Count == 0 || targetPositions.Count == 0) return;
        int d = SkillPipeline.MinCombatDistance(attackerPositions, targetPositions);
        if (d > 1) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        BroadcastCombatLog(new List<CombatLogEntry>
        {
            new()
            {
                LogType = PGame.CombatLogType.CombatLogSkill, Timestamp = (ulong)now,
                ActorName = SkillPipeline.GetEntityName(attackerId, maps),
                SkillName = "普通攻击", ActorId = attackerId,
                MapName = SkillPipeline.GetEntityMapName(attackerId, maps) ?? "",
            }
        }, maps);

        ApplyDamage(attackerId, targetId, 5, "physical", maps);
        _relations.UpdateLastDamageTime(attackerId, targetId);
    }

    // ---- 伤害 ----

    public void ApplyDamage(long attackerId, long targetId, int damage, string damageType, Dictionary<string, MapState>? maps)
    {
        _logger.LogInformation("[Combat] damage: attacker={Attacker} target={Target} dmg={Damage}", attackerId, targetId, damage);

        if (maps != null)
        {
            foreach (var map in maps.Values)
            {
                if (map.Players.TryGetValue(targetId, out var p)) { p.Hp = Math.Max(0, p.Hp - damage); break; }
                if (map.Monsters.TryGetValue(targetId, out var m)) { m.Hp = Math.Max(0, m.Hp - damage); break; }
            }
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var mapsSafe = maps ?? new Dictionary<string, MapState>();
        BroadcastCombatLog(new List<CombatLogEntry>
        {
            new()
            {
                LogType = PGame.CombatLogType.CombatLogDamage, Timestamp = (ulong)now,
                ActorName = SkillPipeline.GetEntityName(attackerId, mapsSafe),
                TargetName = SkillPipeline.GetEntityName(targetId, mapsSafe),
                Value = damage, ActorId = attackerId,
                MapName = SkillPipeline.GetEntityMapName(attackerId, mapsSafe) ?? SkillPipeline.GetEntityMapName(targetId, mapsSafe) ?? "",
            }
        }, mapsSafe);

        _relations.UpdateLastDamageTime(attackerId, targetId);
    }

    public void OnDeath(long entityId)
    {
        _relations.OnEntityRemoved(entityId);
        _logger.LogInformation("[Combat] death: entity={EntityId}", entityId);
    }

    // ---- ATB Tick ----

    public void TickATB(double dt, Dictionary<string, MapState> maps)
    {
        foreach (var (entityId, ctx) in _relations.Contexts)
        {
            if (ctx.State == "COMBAT" && ctx.SubState != "CASTING")
            {
                if (ctx.SubState == "POST_CAST" && ctx.PostCastEndTime.HasValue && Environment.TickCount64 < ctx.PostCastEndTime)
                { /* 后摇中 */ }
                else if (ctx.SubState == "POST_CAST")
                {
                    ctx.SubState = "NONE";
                    ctx.PostCastEndTime = null;
                }

                double agilityCoef = GetAgilityCoefficient(entityId, maps);
                double delta = GameConstants.BaseAtbRate * agilityCoef * (1 + ctx.AtbBoost) * dt;
                ctx.AtbValue = Math.Min(100, ctx.AtbValue + delta);

                if (ctx.AtbValue >= 100)
                {
                    if (ctx.SubState == "POST_CAST" && ctx.PostCastEndTime.HasValue && Environment.TickCount64 < ctx.PostCastEndTime)
                    {
                        ctx.AtbValue = 100;
                    }
                    else
                    {
                        int skillId = SelectSkill(ctx);
                        if (skillId == 0)
                        {
                            ctx.AtbValue = 100; // 所有技能冷却中，待机
                        }
                        else
                        {
                            // 保持 ATB=100，进入蓄力（蓄力完成后才重置）
                            RequestCast(entityId, skillId, maps);
                        }
                    }
                }
            }
            else if (ctx.State == "COMBAT" && ctx.SubState == "CASTING")
            {
                var result = _pipeline.ResumeCast(entityId, maps);
                if (result == "SUCCESS" || result == "MISS")
                {
                    // 蓄力完成（命中或未命中），重置 ATB
                    ctx.AtbValue = 0;
                    ctx.AtbBoost = 0;
                    ctx.AtbBoostStacks = 0;
                }
                if (result == "MISS")
                {
                    ctx.AtbBoost = Math.Min(ctx.AtbBoost + GameConstants.AtbBoostPerMiss, GameConstants.AtbBoostMax);
                    ctx.AtbBoostStacks = Math.Min(ctx.AtbBoostStacks + 1, GameConstants.AtbBoostStacksMax);
                }
            }
        }

        BroadcastCombatState(maps);
    }

    private void RequestCast(long entityId, int skillId, Dictionary<string, MapState> maps)
    {
        var result = _pipeline.Cast(skillId, entityId, maps);
        if (result == "SUCCESS" || result == "MISS")
        {
            // 即时技能（CastTime=0）直接重置 ATB
            var ctx = _relations.Contexts.GetValueOrDefault(entityId);
            if (ctx != null) { ctx.AtbValue = 0; ctx.AtbBoost = 0; ctx.AtbBoostStacks = 0; }
        }
        if (result == "SUCCESS")
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string? skillName = SkillPipeline.GetSkillName(skillId);
            BroadcastCombatLog(new List<CombatLogEntry>
            {
                new()
                {
                    LogType = PGame.CombatLogType.CombatLogSkill,
                    Timestamp = (ulong)now,
                    ActorName = SkillPipeline.GetEntityName(entityId, maps),
                    SkillName = skillName ?? "未知技能",
                    ActorId = entityId,
                    MapName = SkillPipeline.GetEntityMapName(entityId, maps) ?? "",
                }
            }, maps);
        }
        else if (result == "MISS")
        {
            var ctx = _relations.Contexts.GetValueOrDefault(entityId);
            if (ctx != null)
            {
                ctx.AtbBoost = Math.Min(ctx.AtbBoost + GameConstants.AtbBoostPerMiss, GameConstants.AtbBoostMax);
                ctx.AtbBoostStacks = Math.Min(ctx.AtbBoostStacks + 1, GameConstants.AtbBoostStacksMax);
            }
        }
        else if (result == "FAILURE")
        {
            var ctx = _relations.Contexts.GetValueOrDefault(entityId);
            if (ctx != null)
            {
                ctx.AtbBoost = 0.15;
                ctx.AtbBoostStacks = 1;
            }
        }
    }

    // ---- 怪物回血 ----

    public void TickMonsterRegen(double dt, IMonsterRegistry? monsterRegistry)
    {
        foreach (var (entityId, ctx) in _relations.Contexts)
        {
            if (ctx.State != "IDLE" || entityId < 1000000) continue;
            int regen = (int)(100 * GameConstants.MonsterRegenPercentPerSec * dt);
            if (regen > 0 && monsterRegistry != null)
                monsterRegistry.OnRegen(entityId, regen);
        }
    }

    // ---- 主 Tick ----

    public void Tick(double dt, Dictionary<string, MapState> maps, IMonsterRegistry? monsterRegistry)
    {
        var disengaged = _disengage.Tick(dt, _relations, maps);
        if (disengaged.Count > 0)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var endLogs = new List<CombatLogEntry>();
            foreach (var (_, attackerId, targetId) in disengaged)
            {
                string mapName = SkillPipeline.GetEntityMapName(attackerId, maps) ?? SkillPipeline.GetEntityMapName(targetId, maps) ?? "";
                endLogs.Add(new()
                {
                    LogType = PGame.CombatLogType.CombatLogEnd,
                    Timestamp = (ulong)now,
                    ActorName = SkillPipeline.GetEntityName(attackerId, maps),
                    TargetName = SkillPipeline.GetEntityName(targetId, maps),
                    Extra = "战斗结束，已脱战",
                    ActorId = attackerId,
                    MapName = mapName,
                });
            }
            BroadcastCombatLog(endLogs, maps);

            // 通知怪物脱战，恢复 AI 行为
            foreach (var (_, attackerId, targetId) in disengaged)
            {
                if (monsterRegistry != null)
                {
                    if (attackerId >= 1000000) monsterRegistry.OnDisengage(attackerId);
                    if (targetId >= 1000000) monsterRegistry.OnDisengage(targetId);
                }
            }
        }
        TickATB(dt, maps);
        TickMonsterRegen(dt, monsterRegistry);
    }

    // ---- 辅助 ----

    private static double GetAgilityCoefficient(long entityId, Dictionary<string, MapState> maps)
    {
        foreach (var map in maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
            {
                int agility = p.Agility > 0 ? p.Agility : 100;
                return 1.0 + (agility - 100) * 0.01;
            }
            if (map.Monsters.TryGetValue(entityId, out var m))
            {
                int agility = m.Agility > 0 ? m.Agility : 100;
                return 1.0 + (agility - 100) * 0.01;
            }
        }
        return 1.0;
    }

    private static int SelectSkill(CombatContext ctx)
    {
        long now = Environment.TickCount64;
        foreach (var skillId in ctx.SkillPool)
        {
            if (!ctx.SkillCooldowns.TryGetValue(skillId, out var cdEnd) || now >= cdEnd)
                return skillId;
        }
        return ctx.SkillPool.Count > 0 ? 0 : 1;
    }

    private static void SetCombatJob(CombatContext ctx, long entityId, Dictionary<string, MapState> maps)
    {
        if (ctx.Job != "") return;
        foreach (var map in maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
            {
                ctx.Job = p.Job ?? "";
                if (ctx.Job == "战士")
                    ctx.SkillPool = new List<int> { 2, 3, 4 };
                return;
            }
        }
        ctx.Job = "monster";
        ctx.SkillPool = new List<int> { 1 };
    }

    private void BroadcastCombatState(Dictionary<string, MapState> maps)
    {
        var playerStates = new Dictionary<long, List<(long id, string name, double atb, bool isPlayer, int hp, int maxHp, string castingSkill, float castProgress)>>();

        foreach (var (mapName, map) in maps)
        {
            foreach (var (accountId, p) in map.Players)
            {
                var ctx = _relations.Contexts.GetValueOrDefault(accountId);
                if (ctx == null || ctx.State != "COMBAT") continue;

                var (cs, cp) = GetCastingInfo(ctx);
                var unitSet = new HashSet<long> { accountId };
                var units = new List<(long, string, double, bool, int, int, string, float)>
                {
                    (accountId, p.RoleName ?? $"player_{accountId}", ctx.AtbValue, true, p.Hp, p.MaxHp, cs, cp)
                };

                foreach (var relationId in ctx.RelationIds)
                {
                    var rel = _relations.Relations.GetValueOrDefault(relationId);
                    if (rel == null || !rel.IsActive) continue;
                    long otherId = rel.AttackerId == accountId ? rel.TargetId : rel.AttackerId;
                    if (!unitSet.Add(otherId)) continue;

                    var otherCtx = _relations.Contexts.GetValueOrDefault(otherId);
                    int otherHp = 0, otherMaxHp = 0;
                    foreach (var m2 in maps.Values)
                    {
                        if (m2.Players.TryGetValue(otherId, out var op2)) { otherHp = op2.Hp; otherMaxHp = op2.MaxHp; break; }
                        if (m2.Monsters.TryGetValue(otherId, out var om2)) { otherHp = om2.Hp; otherMaxHp = om2.MaxHp; break; }
                    }
                    var (ocs, ocp) = GetCastingInfo(otherCtx);

                    units.Add((otherId, SkillPipeline.GetEntityName(otherId, maps),
                        otherCtx?.AtbValue ?? 0, otherId < 1000000, otherHp, otherMaxHp, ocs, ocp));
                }

                if (units.Count > 0) playerStates[accountId] = units;
            }
        }

        foreach (var (accountId, units) in playerStates)
        {
            string? mapName = SkillPipeline.GetEntityMapName(accountId, maps);
            if (mapName == null) continue;
            int serverId = maps[mapName].Players.GetValueOrDefault(accountId)?.ServerId ?? 0;

            var notify = new PGame.CombatStateNotify();
            foreach (var (id, name, atb, isPlayer, hp, maxHp, castingSkill, castProgress) in units)
                notify.Units.Add(new PGame.CombatStateNotify.Types.CombatUnit
                {
                    EntityId = (ulong)id, EntityName = name, Atb = (float)atb,
                    IsPlayer = isPlayer, Hp = hp, MaxHp = maxHp,
                    CastingSkill = castingSkill, CastProgress = castProgress,
                });

            _network.SendToAccount(accountId, serverId, (int)PProtocol.MessageId.GameCombatStateNotify, notify.ToByteArray());
        }

        // IDLE 玩家发空列表
        foreach (var (_, map) in maps)
        {
            foreach (var (accountId, p) in map.Players)
            {
                if (playerStates.ContainsKey(accountId)) continue;
                var ctx = _relations.Contexts.GetValueOrDefault(accountId);
                if (ctx != null && ctx.State == "IDLE")
                    _network.SendToAccount(accountId, p.ServerId, (int)PProtocol.MessageId.GameCombatStateNotify, new PGame.CombatStateNotify().ToByteArray());
            }
        }
    }

    private static (string skill, float progress) GetCastingInfo(CombatContext? ctx)
    {
        if (ctx == null || ctx.SubState != "CASTING" || ctx.CastSkillId == null || ctx.CastEndTime == null)
            return ("", 0f);

        var cfg = SkillPipeline.GetSkillConfig(ctx.CastSkillId.Value);
        string skillName = cfg?.Name ?? "";
        long totalMs = (long)((cfg?.CastTime ?? 0.5) * 1000);
        if (totalMs <= 0) return (skillName, 1f);
        long startTime = ctx.CastEndTime.Value - totalMs;
        float progress = Math.Clamp((float)(Environment.TickCount64 - startTime) / totalMs, 0f, 1f);
        return (skillName, progress);
    }

    private void BroadcastCombatLog(List<CombatLogEntry> entries, Dictionary<string, MapState> maps)
    {
        if (entries.Count == 0) return;

        var mapSet = new HashSet<string>();
        foreach (var e in entries)
        {
            var mn = e.MapName ?? SkillPipeline.GetEntityMapName(e.ActorId, maps);
            if (mn != null) mapSet.Add(mn);
        }

        foreach (var mapName in mapSet)
        {
            if (!maps.TryGetValue(mapName, out var map)) continue;
            var notify = new PGame.CombatLogNotify();
            foreach (var e in entries)
            {
                if ((e.MapName ?? SkillPipeline.GetEntityMapName(e.ActorId, maps)) != mapName) continue;
                notify.Entries.Add(new PGame.CombatLogEntry
                {
                    LogType = e.LogType, Timestamp = e.Timestamp,
                    ActorName = e.ActorName, TargetName = e.TargetName,
                    SkillName = e.SkillName, Value = e.Value, Extra = e.Extra,
                });
            }
            if (notify.Entries.Count > 0)
            {
                var data = notify.ToByteArray();
                foreach (var (accountId, p) in map.Players)
                    _network.SendToAccount(accountId, p.ServerId, (int)PProtocol.MessageId.GameCombatLogNotify, data);
            }
        }
    }
}
