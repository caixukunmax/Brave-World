using System.Collections.Concurrent;
using GameServer.Services.Core;
using GameServer.Services.Map.Combat.Actions;
using GameServer.Services.World;
using GameServer.Tables;
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
    private readonly List<long> _pendingDisengages = new();
    private readonly ConcurrentQueue<(long entityA, long entityB)> _forceDisengagePairs = new();
    private readonly Dictionary<long, int> _lastBuffCount = new(); // 上一帧每个实体的 buff 数量
    private readonly Dictionary<long, int> _lastShieldAmount = new(); // 上一帧每个实体的护盾总量
    private readonly LubanTableLoader? _tables;
    private readonly Dictionary<long, double> _mpRegenAccum = new();
    private readonly Dictionary<long, double> _hpRegenAccum = new();
    private readonly Dictionary<long, (int hp, int mp)> _lastSyncedHpMp = new();
    private readonly CombatNarrationEngine? _narration;
    private readonly ProjectileManager _projectileMgr;

    /// <summary>战斗序号 → 战斗ID，用于日志追踪</summary>
    private readonly Dictionary<long, long> _entityCombatId = new();
    private long _nextCombatId = 1;

    /// <summary>分配下一个战斗 ID</summary>
    private long AllocCombatId() => Interlocked.Increment(ref _nextCombatId);

    /// <summary>获取地形 HP 恢复/伤害值（正=恢复，负=伤害）</summary>
    private int GetTerrainHpRegen(long entityId, string mapName, int gridX, int gridY)
    {
        if (MapData == null || _tables == null) return 0;
        int terrainId = MapData.GetTerrainType(mapName, gridX, gridY);
        var cfg = _tables.GetTerrainConfig(terrainId);
        return cfg?.HpRegenPerSec ?? 0;
    }

    /// <summary>获取地形 MP 恢复值</summary>
    private int GetTerrainMpRegen(long entityId, string mapName, int gridX, int gridY)
    {
        if (MapData == null || _tables == null) return 0;
        int terrainId = MapData.GetTerrainType(mapName, gridX, gridY);
        var cfg = _tables.GetTerrainConfig(terrainId);
        return cfg?.MpRegenPerSec ?? 0;
    }

    /// <summary>获取地形防御修正系数（1.0=无修正）</summary>
    internal float GetTerrainDefModifier(long entityId, string mapName, int gridX, int gridY, string damageType)
    {
        if (MapData == null || _tables == null) return 1.0f;
        int terrainId = MapData.GetTerrainType(mapName, gridX, gridY);
        var cfg = _tables.GetTerrainConfig(terrainId);
        if (cfg == null) return 1.0f;
        return damageType switch
        {
            "physical" => cfg.PdefModifier,
            "magical" => cfg.MdefModifier,
            _ => 1.0f,
        };
    }

    public CombatRelationManager RelationsMgr => _relations;

    /// <summary>请求强制脱战（如怪物追击超时），由外部系统调用</summary>
    public void RequestDisengage(long entityA, long entityB)
    {
        _forceDisengagePairs.Enqueue((entityA, entityB));
    }

    /// <summary>日志器（供 Action 输出 CombatTrace）</summary>
    public ILogger Logger => _logger;

    /// <summary>玩家死亡回调 — 由外部 DeathResponder 绑定</summary>
    public Action<long, Dictionary<string, MapState>>? DeathCallback { get; set; }

    /// <summary>怪物注册接口 — 供战斗伤害通知怪物进入战斗状态</summary>
    public IMonsterRegistry? MonsterRegistry { get; set; }

    /// <summary>地图数据提供者 — 用于查询地形</summary>
    public GameServer.Common.Config.MapDataProvider? MapData { get; set; }

    public CombatManager(
        ILogger<CombatManager> logger,
        ILoggerFactory loggerFactory,
        SkillPipeline pipeline,
        ActionRegistry actionRegistry,
        INetworkSender network,
        LubanTableLoader? tables = null)
    {
        _logger = logger;
        _pipeline = pipeline;
        _network = network;
        _relations = new CombatRelationManager(tables);
        _disengage = new DisengageSystem(logger);
        _tables = tables;

        // 绑定 pipeline 反向引用（Phase 3 后续会移除）
        _pipeline.CombatManager = this;
        _pipeline.ProjectileManager = _projectileMgr = new ProjectileManager(loggerFactory.CreateLogger<ProjectileManager>());

        // 加载战斗日志文本模板
        if (tables != null)
        {
            CombatLogFormatter.Load(tables.CombatLogTexts);
            _narration = new CombatNarrationEngine(tables);
        }
    }

    // ---- 碰撞入口 ----

    public bool IsCasting(long entityId)
    {
        var ctx = _relations.Contexts.GetValueOrDefault(entityId);
        return ctx != null && ctx.SubState == "CASTING";
    }

    /// <summary>获取实体的战斗上下文（供 GM 命令等外部调用）</summary>
    public CombatContext? GetContext(long entityId)
    {
        return _relations.Contexts.GetValueOrDefault(entityId);
    }

    /// <summary>获取实体的战斗 ID（供日志追踪）</summary>
    public long GetCombatId(long entityId)
    {
        return _entityCombatId.GetValueOrDefault(entityId);
    }

    public void OnCollision(long entityA, long entityB, Dictionary<string, MapState> maps)
    {
        if (_relations.HasActiveRelation(entityA, entityB))
        {
            _logger.LogDebug("[Combat] onCollision skipped: A={A} B={B} already in combat", entityA, entityB);
            return;
        }

        _logger.LogInformation("[Combat] onCollision: A={A} B={B}", entityA, entityB);

        var combatId = AllocCombatId();
        _entityCombatId[entityA] = combatId;
        _entityCombatId[entityB] = combatId;

        var nameA = SkillPipeline.GetEntityName(entityA, maps);
        var nameB = SkillPipeline.GetEntityName(entityB, maps);
        CombatTrace.CombatStart(_logger, combatId, entityA, entityB, nameA, nameB);

        _relations.CreateRelation(entityA, entityB);
        _relations.CreateRelation(entityB, entityA);

        var ctxA = _relations.GetOrCreateContext(entityA, FindPlayerBuffs(entityA, maps));
        var ctxB = _relations.GetOrCreateContext(entityB, FindPlayerBuffs(entityB, maps));

        SetCombatJob(ctxA, entityA, maps);
        SetCombatJob(ctxB, entityB, maps);

        // 刷新挑衅期：每次碰撞都刷新双向挑衅期
        _relations.RefreshTaunt(entityA, entityB);

        // 先手攻击：仅当双方此前均未建立过任何战斗关系，且主动碰撞方(entityA)是玩家时才触发
        bool aIsPlayer = entityA < CombatConstants.MonsterIdThreshold;
        bool bothWereIdle = ctxA.RelationIds.Count == 0 && ctxB.RelationIds.Count == 0;
        if (aIsPlayer && bothWereIdle)
        {
            ExecuteFirstStrike(entityA, entityB, maps);
        }

        _relations.SetState(entityA, "COMBAT");
        _relations.SetState(entityB, "COMBAT");

        // 设置 MapPlayerState.InCombat
        SetPlayerInCombat(entityA, true, maps);
        SetPlayerInCombat(entityB, true, maps);

        // 发送 CombatStartNotify 给参战玩家
        SendCombatStartNotify(entityA, entityB, maps);

        // 广播战斗开始
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string mapName = SkillPipeline.GetEntityMapName(entityA, maps) ?? SkillPipeline.GetEntityMapName(entityB, maps) ?? "";
        string actorA = SkillPipeline.GetEntityName(entityA, maps);
        string actorB = SkillPipeline.GetEntityName(entityB, maps);

        // 叙事触发：战斗开始
        TryNarrate("combat_start", entityA, entityB, actorA, actorB, 0, maps);

        BroadcastCombatLog(new List<CombatLogEntry>
        {
            new()
            {
                LogType = PGame.CombatLogType.CombatLogStart, Timestamp = (ulong)now,
                ActorName = actorA, TargetName = actorB,
                Extra = CombatLogFormatter.Format(0, actorA, actorB, null, 0, null),
                ActorId = entityA, MapName = mapName,
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

        var combatId = _entityCombatId.GetValueOrDefault(attackerId);
        CombatTrace.FirstStrike(_logger, combatId, attackerId, SkillPipeline.GetEntityName(attackerId, maps), targetId, SkillPipeline.GetEntityName(targetId, maps), 1);

        // 先手攻击：设置先攻加速属性，首个技能释放时应用提速（不在此处执行伤害）
        var ctx = _relations.GetOrCreateContext(attackerId);
        ctx.FirstStrikeHaste = _tables?.GetPlayerFirstStrikeHaste() ?? 30;
        ctx.HasUsedFirstStrike = false;
    }

    /// <summary>普攻回退：skill 1 未配置时直接计算伤害</summary>
    private static (int damage, string damageType) CalcDirectDamage(long casterId, long targetId, Dictionary<string, MapState> maps)
    {
        int patk = DealDamageAction.GetEntityAttr(casterId, "patk", maps) ?? 10;
        int pdef = DealDamageAction.GetEntityAttr(targetId, "pdef", maps) ?? 5;
        int damage = (int)Math.Floor(patk * 1.0 * (1 - pdef * 0.01));
        if (damage < 1) damage = 1;
        return (damage, "physical");
    }

    // ---- 伤害 ----

    public void ApplyDamage(long attackerId, long targetId, int damage, string damageType, Dictionary<string, MapState>? maps)
    {
        var mapsSafe = maps ?? new Dictionary<string, MapState>();
        var combatId = _entityCombatId.GetValueOrDefault(attackerId);
        string actorName = SkillPipeline.GetEntityName(attackerId, mapsSafe);
        string targetName = SkillPipeline.GetEntityName(targetId, mapsSafe);

        _logger.LogInformation("[Combat] damage: attacker={Attacker} target={Target} dmg={Damage}", attackerId, targetId, damage);

        // 先广播伤害日志（死亡处理前，避免重生后收到多余日志）
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        BroadcastCombatLog(new List<CombatLogEntry>
        {
            new()
            {
                LogType = PGame.CombatLogType.CombatLogDamage, Timestamp = (ulong)now,
                ActorName = actorName, TargetName = targetName,
                Value = damage, ActorId = attackerId,
                Extra = CombatLogFormatter.Format(2, actorName, targetName, null, damage, null),
                MapName = SkillPipeline.GetEntityMapName(attackerId, mapsSafe) ?? SkillPipeline.GetEntityMapName(targetId, mapsSafe) ?? "",
            }
        }, mapsSafe);

        _relations.UpdateLastDamageTime(attackerId, targetId);
        _relations.AddAccumulatedDamage(attackerId, targetId, damage);

        // 叙事触发：首次被击中（LastDamageTime=0 说明是第一次受伤）
        foreach (var rel in _relations.Relations.Values)
        {
            if ((rel.AttackerId == attackerId && rel.TargetId == targetId) ||
                (rel.AttackerId == targetId && rel.TargetId == attackerId))
            {
                if (rel.LastDamageTime == 0)
                    TryNarrate("first_hit", attackerId, targetId, actorName, targetName, 0, mapsSafe);
                break;
            }
        }

        // 叙事触发：伤害
        TryNarrate("damage_taken", attackerId, targetId, actorName, targetName, damage, mapsSafe);

        // 护盾吸收
        int absorbed = 0;
        MapPlayerState? targetPlayer = null;
        if (maps != null)
        {
            foreach (var map in maps.Values)
            {
                if (map.Players.TryGetValue(targetId, out var p))
                {
                    absorbed = p.Buffs.AbsorbShield(damage);
                    targetPlayer = p;
                    break;
                }
                // 怪物的护盾在 CombatContext 里，后续补
            }
        }
        int effectiveDamage = damage - absorbed;
        if (effectiveDamage < 0) effectiveDamage = 0;
        if (absorbed > 0)
        {
            _logger.LogInformation("[Combat] shield absorbed: target={Target} absorbed={Absorbed} effective={Effective}", targetId, absorbed, effectiveDamage);
            CombatTrace.BuffShieldAbsorb(_logger, combatId, targetId, targetName, damage, absorbed, targetPlayer?.Buffs.GetShieldAmount() ?? 0);
        }

        // 广播战斗事件（伤害）
        if (maps != null)
            BroadcastCombatEventNotify(targetId, -effectiveDamage, 0, maps);

        // 扣血 + 死亡处理（通过 CombatEntityState 基类统一）
        if (maps != null)
        {
            foreach (var map in maps.Values)
            {
                CombatEntityState? target = null;
                if (map.Players.TryGetValue(targetId, out var p))
                    target = p;
                else if (map.Monsters.TryGetValue(targetId, out var m))
                {
                    target = m;
                    // 通知 MonsterManager 设置 InCombat = true 并记录伤害
                    MonsterRegistry?.OnDamage(targetId, attackerId, damage);
                }
                else if (map.Npcs.TryGetValue(targetId, out var n))
                    target = n;  // NPC 也可以被打

                if (target != null)
                {
                    int hpBefore = target.Hp;
                    target.Hp = Math.Max(0, target.Hp - effectiveDamage);
                    CombatTrace.DamageApply(_logger, combatId, attackerId, actorName, targetId, targetName, damage, damageType, absorbed, effectiveDamage);
                    // 叙事触发：HP 低于阈值
                    if (target.MaxHp > 0)
                        TryNarrate("hp_below_pct", attackerId, targetId, actorName, targetName, (double)target.Hp / target.MaxHp, mapsSafe);
                    if (target.Hp == 0)
                    {
                        CombatTrace.Death(_logger, combatId, targetId, targetName, hpBefore);
                        OnDeath(targetId, maps);
                        DeathCallback?.Invoke(targetId, maps);
                    }
                    break;
                }
            }
        }
    }

    public void OnDeath(long entityId, Dictionary<string, MapState>? maps)
    {
        var mapsSafe = maps ?? new Dictionary<string, MapState>();
        string actorName = SkillPipeline.GetEntityName(entityId, mapsSafe);
        string mapName = SkillPipeline.GetEntityMapName(entityId, mapsSafe) ?? "";

        // 收集战斗中的怪物ID，用于后续通知脱战；同时记录击杀者信息（OnEntityRemoved 会清空 RelationIds）
        var ctx = _relations.Contexts.GetValueOrDefault(entityId);
        long killerId = 0;
        string killerName = "";
        if (ctx != null)
        {
            foreach (var relationId in ctx.RelationIds)
            {
                var rel = _relations.Relations.GetValueOrDefault(relationId);
                if (rel == null || !rel.IsActive) continue;
                long otherId = rel.AttackerId == entityId ? rel.TargetId : rel.AttackerId;
                if (otherId >= CombatConstants.MonsterIdThreshold) _pendingDisengages.Add(otherId);
                if (killerId == 0)
                {
                    killerId = otherId;
                    killerName = SkillPipeline.GetEntityName(killerId, mapsSafe);
                }
            }
        }

        // 死亡时清除一次性优先技能（必须在移除 CombatContext 之前执行）
        if (entityId < 1000000)
            ClearPreferredSkill(entityId, mapsSafe);

        _relations.OnEntityRemoved(entityId);

        // NPC 死亡/脱战：重置 InCombat
        if (entityId < 1000000)
        {
            foreach (var map in mapsSafe.Values)
            {
                if (map.Npcs.TryGetValue(entityId, out var npc))
                    npc.InCombat = false;
                if (map.Players.TryGetValue(entityId, out var p))
                    p.InCombat = false;
            }
        }

        // 广播死亡日志
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        BroadcastCombatLog(new List<CombatLogEntry>
        {
            new()
            {
                LogType = PGame.CombatLogType.CombatLogDeath, Timestamp = (ulong)now,
                ActorName = actorName,
                Extra = CombatLogFormatter.Format(6, actorName, null, null, 0, "已阵亡"),
                ActorId = entityId, MapName = mapName,
            }
        }, mapsSafe);

        // 叙事触发：击杀（击杀者信息在 OnEntityRemoved 前已收集）
        if (killerId != 0)
        {
            TryNarrate("kill", killerId, entityId, killerName, actorName, 0, mapsSafe);
        }

        // 立即发送空的战斗状态给死亡玩家，清除客户端战斗面板
        if (maps != null)
        {
            foreach (var map in maps.Values)
            {
                if (map.Players.TryGetValue(entityId, out var p))
                {
                    _network.SendToAccount(entityId, p.ServerId,
                        (int)PProtocol.MessageId.GameCombatStateNotify,
                        new PGame.CombatStateNotify().ToByteArray());
                    break;
                }
            }
        }

        _logger.LogInformation("[Combat] death: entity={EntityId}", entityId);
    }

    // ---- 治疗 ----

    public void ApplyHeal(long casterId, long targetId, int healAmount, string healType, Dictionary<string, MapState>? maps)
    {
        _logger.LogInformation("[Combat] heal: caster={Caster} target={Target} amount={Amount}", casterId, targetId, healAmount);

        var mapsSafe = maps ?? new Dictionary<string, MapState>();

        // 广播治疗日志
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string actorName = SkillPipeline.GetEntityName(casterId, mapsSafe);
        BroadcastCombatLog(new List<CombatLogEntry>
        {
            new()
            {
                LogType = PGame.CombatLogType.CombatLogHeal, Timestamp = (ulong)now,
                ActorName = actorName,
                Value = healAmount, ActorId = casterId,
                Extra = CombatLogFormatter.Format(3, actorName, null, null, healAmount, null),
                MapName = SkillPipeline.GetEntityMapName(casterId, mapsSafe) ?? "",
            }
        }, mapsSafe);

        // 广播战斗事件（治疗）
        if (maps != null)
            BroadcastCombatEventNotify(targetId, healAmount, 0, maps);

        // 回血（不超过上限）
        if (maps != null)
        {
            foreach (var map in maps.Values)
            {
                CombatEntityState? target = null;
                if (map.Players.TryGetValue(targetId, out var p))
                    target = p;
                else if (map.Monsters.TryGetValue(targetId, out var m))
                    target = m;
                else if (map.Npcs.TryGetValue(targetId, out var n))
                    target = n;

                if (target != null)
                {
                    target.Hp = Math.Min(target.MaxHp, target.Hp + healAmount);
                    break;
                }
            }
        }
    }

    // ---- CD 驱动战斗 ----

    /// <summary>同步玩家 PreferredSkillId 到 CombatContext（每帧检查）</summary>
    private void SyncPreferredSkills(Dictionary<string, MapState> maps)
    {
        foreach (var ctx in _relations.Contexts.Values)
        {
            if (ctx.EntityId >= CombatConstants.MonsterIdThreshold) continue; // 怪物跳过
            foreach (var map in maps.Values)
            {
                if (map.Players.TryGetValue(ctx.EntityId, out var p))
                {
                    ctx.PreferredSkillId = p.PreferredSkillId;
                    break;
                }
            }
        }
    }

    /// <summary>战斗实体 CD 驱动自动施法 — 纯 CD 制下按冷却自动释放技能</summary>
    private void TickMonsterSkills(double dt, Dictionary<string, MapState> maps)
    {
        long now = Environment.TickCount64;
        foreach (var (entityId, ctx) in _relations.Contexts.ToList())
        {
            if (ctx.State != "COMBAT") continue;

            if (ctx.SubState != "CASTING")
            {
                if (ctx.SubState == "POST_CAST" && ctx.PostCastEndTime.HasValue && now < ctx.PostCastEndTime)
                { /* 后摇中 */ }
                else
                {
                    if (ctx.SubState == "POST_CAST")
                    {
                        ctx.SubState = "NONE";
                        ctx.PostCastEndTime = null;
                    }

                    // 处理所有战斗实体自动施法（玩家 + 怪物）
                    int skillId = SelectSkillWithLog(ctx, entityId, maps);
                    if (skillId > 0)
                    {
                        RequestCast(entityId, skillId, maps);
                    }
                }
            }
            else if (ctx.SubState == "CASTING")
            {
                int? pendingSkillId = ctx.CastSkillId;
                var result = _pipeline.ResumeCast(entityId, maps);
                if (result == "SUCCESS")
                {
                    long nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    string actorName = SkillPipeline.GetEntityName(entityId, maps);
                    string sName = pendingSkillId.HasValue ? SkillPipeline.GetSkillNameStatic(pendingSkillId.Value) ?? "未知技能" : "未知技能";
                    BroadcastCombatLog(new List<CombatLogEntry>
                    {
                        new()
                        {
                            LogType = PGame.CombatLogType.CombatLogSkill,
                            Timestamp = (ulong)nowSec,
                            ActorName = actorName, SkillName = sName,
                            Extra = CombatLogFormatter.Format(1, actorName, null, sName, 0, null),
                            ActorId = entityId,
                            MapName = SkillPipeline.GetEntityMapName(entityId, maps) ?? "",
                        }
                    }, maps);
                }
                if (result == "MISS")
                {
                    long nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    string actorName = SkillPipeline.GetEntityName(entityId, maps);
                    string sName = pendingSkillId.HasValue ? SkillPipeline.GetSkillNameStatic(pendingSkillId.Value) ?? "未知技能" : "未知技能";
                    BroadcastCombatLog(new List<CombatLogEntry>
                    {
                        new()
                        {
                            LogType = PGame.CombatLogType.CombatLogDodge,
                            Timestamp = (ulong)nowSec,
                            ActorName = actorName, SkillName = sName,
                            Extra = CombatLogFormatter.Format(5, actorName, null, sName, 0, null),
                            ActorId = entityId,
                            MapName = SkillPipeline.GetEntityMapName(entityId, maps) ?? "",
                        }
                    }, maps);
                }
            }
        }

        BroadcastCombatState(maps);
    }

    private void ClearPreferredSkillIfCast(long entityId, int skillId, Dictionary<string, MapState> maps)
    {
        var ctx = _relations.Contexts.GetValueOrDefault(entityId);
        if (ctx == null || ctx.PreferredSkillId != skillId) return;

        ctx.PreferredSkillId = 0;

        // 同步回 MapPlayerState
        string? mapName = SkillPipeline.GetEntityMapName(entityId, maps);
        if (mapName != null && maps.TryGetValue(mapName, out var map))
        {
            if (map.Players.TryGetValue(entityId, out var p))
                p.PreferredSkillId = 0;
        }
    }

    /// <summary>清除一次性优先技能（死亡/脱战时调用）</summary>
    private void ClearPreferredSkill(long entityId, Dictionary<string, MapState> maps)
    {
        var ctx = _relations.Contexts.GetValueOrDefault(entityId);
        if (ctx != null)
            ctx.PreferredSkillId = 0;

        string? mapName = SkillPipeline.GetEntityMapName(entityId, maps);
        if (mapName != null && maps.TryGetValue(mapName, out var map))
        {
            if (map.Players.TryGetValue(entityId, out var p))
                p.PreferredSkillId = 0;
        }
    }

    private void RequestCast(long entityId, int skillId, Dictionary<string, MapState> maps)
    {
        var result = _pipeline.Cast(skillId, entityId, maps);
        if (result == "SUCCESS")
        {
            ClearPreferredSkillIfCast(entityId, skillId, maps);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string? skillName = SkillPipeline.GetSkillNameStatic(skillId);
            string actorName = SkillPipeline.GetEntityName(entityId, maps);
            string sName = skillName ?? "未知技能";
            BroadcastCombatLog(new List<CombatLogEntry>
            {
                new()
                {
                    LogType = PGame.CombatLogType.CombatLogSkill,
                    Timestamp = (ulong)now,
                    ActorName = actorName, SkillName = sName,
                    Extra = CombatLogFormatter.Format(1, actorName, null, sName, 0, null),
                    ActorId = entityId,
                    MapName = SkillPipeline.GetEntityMapName(entityId, maps) ?? "",
                }
            }, maps);
        }
        else if (result == "MISS")
        {
            ClearPreferredSkillIfCast(entityId, skillId, maps);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string actorName = SkillPipeline.GetEntityName(entityId, maps);
            string sName = SkillPipeline.GetSkillNameStatic(skillId) ?? "未知技能";
            BroadcastCombatLog(new List<CombatLogEntry>
            {
                new()
                {
                    LogType = PGame.CombatLogType.CombatLogDodge,
                    Timestamp = (ulong)now,
                    ActorName = actorName, SkillName = sName,
                    Extra = CombatLogFormatter.Format(5, actorName, null, sName, 0, null),
                    ActorId = entityId,
                    MapName = SkillPipeline.GetEntityMapName(entityId, maps) ?? "",
                }
            }, maps);
        }
        else if (result == "FAILURE")
        {
            _logger.LogWarning("[Combat] RequestCast FAILURE: entity={EntityId} skill={SkillId}", entityId, skillId);
        }
        else if (result == "PENDING")
        {
            ClearPreferredSkillIfCast(entityId, skillId, maps);
            // 读条中，无需额外日志（SkillStartCast 已在 Cast 中输出）
        }
    }

    /// <summary>处理玩家手动/中断施法请求 — 纯 CD 即时制</summary>
    public PGame.CastResponse HandleCastRequest(long playerId, int skillId, bool interrupt, long? targetId, Dictionary<string, MapState> maps)
    {
        var ctx = _relations.Contexts.GetValueOrDefault(playerId);
        if (ctx == null || ctx.State != "COMBAT")
            return new PGame.CastResponse { Success = false, Error = "not_in_combat" };

        if (ctx.SubState == "CASTING" || ctx.SubState == "POST_CAST")
        {
            // 后摇时间已过：视为 IDLE，允许直接施法，避免依赖 tick 调度
            if (ctx.SubState == "POST_CAST" &&
                (!ctx.PostCastEndTime.HasValue || Environment.TickCount64 >= ctx.PostCastEndTime))
            {
                ctx.SubState = "NONE";
                ctx.PostCastEndTime = null;
            }
            else if (!interrupt)
            {
                return new PGame.CastResponse { Success = false, Error = ctx.SubState == "CASTING" ? "already_casting" : "post_cast" };
            }
        }

        if (!ctx.SkillPool.Contains(skillId))
            return new PGame.CastResponse { Success = false, Error = "invalid_skill" };

        var (ok, err) = _pipeline.PreCheck(skillId, ctx, playerId, maps);
        if (!ok)
            return new PGame.CastResponse { Success = false, Error = err == "insufficient_mp" ? "no_mp" : err ?? "cast_failed" };

        var targets = _pipeline.SelectTargets(skillId, playerId, ctx, maps);
        if (targets == null || targets.Count == 0)
            return new PGame.CastResponse { Success = false, Error = "no_target" };

        // 校验通过后再中断当前施法，避免无效请求白白打断当前读条/后摇
        if (ctx.SubState == "CASTING" || ctx.SubState == "POST_CAST")
        {
            _pipeline.InterruptCast(playerId);
        }

        var result = _pipeline.Cast(skillId, playerId, maps);

        if (result == "SUCCESS" || result == "PENDING")
        {
            ClearPreferredSkillIfCast(playerId, skillId, maps);
            return new PGame.CastResponse { Success = true };
        }
        if (result == "MISS")
        {
            ClearPreferredSkillIfCast(playerId, skillId, maps);
            return new PGame.CastResponse { Success = true };
        }

        return new PGame.CastResponse { Success = false, Error = "cast_failed" };
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

    /// <summary>
    /// 玩家 HP 恢复 Tick（脱战才回 + 地形效果）
    /// 脱战后：MaxHp * PlayerHpRegenPercentPerSec /秒
    /// 地形效果（战斗中也有）：草地+1HP/s、神圣地+5HP/s、沼泽-2HP/s、岩浆-5HP/s
    /// </summary>
    public void TickPlayerHpRegen(double dt, Dictionary<string, MapState> maps)
    {
        var alive = new HashSet<long>();
        foreach (var (mapName, map) in maps)
        {
            foreach (var (_, p) in map.Players)
            {
                alive.Add(p.AccountId);
                
                // ---- 地形效果（战斗中/脱战都有）----
                int terrainHpDelta = GetTerrainHpRegen(p.AccountId, mapName, p.GridX, p.GridY);
                if (terrainHpDelta != 0)
                {
                    int terrainHpChange = (int)(terrainHpDelta * dt);
                    if (terrainHpChange != 0)
                    {
                        p.Hp = Math.Min(p.MaxHp, Math.Max(1, p.Hp + terrainHpChange));
                        // TODO: 推送地形伤害/恢复通知给客户端
                    }
                }

                if (p.Hp >= p.MaxHp)
                {
                    _hpRegenAccum.Remove(p.AccountId);
                    continue;
                }

                // 战斗中不回血（自然恢复）
                var ctx = _relations.Contexts.GetValueOrDefault(p.AccountId);
                if (ctx != null && ctx.State == "COMBAT")
                {
                    _hpRegenAccum.Remove(p.AccountId);
                    continue;
                }

                double regenPerSec = p.MaxHp * GameConstants.PlayerHpRegenPercentPerSec;
                double accum = _hpRegenAccum.GetValueOrDefault(p.AccountId) + regenPerSec * dt;
                int regen = (int)accum;
                if (regen > 0)
                {
                    p.Hp = Math.Min(p.MaxHp, p.Hp + regen);
                    accum -= regen;
                }
                _hpRegenAccum[p.AccountId] = accum;
            }
        }

        // 清理已离线玩家的累积器
        foreach (var id in _hpRegenAccum.Keys.ToList())
        {
            if (!alive.Contains(id))
                _hpRegenAccum.Remove(id);
        }
    }

    /// <summary>
    /// 玩家 MP 恢复 Tick（使用浮点累积器避免整数截断）
    /// 战斗中：mp_regen * CombatMpRegenMultiplier /秒
    /// 脱战后：mp_regen * OutOfCombatMpRegenMultiplier /秒
    /// 无 mp_regen 属性时：MaxMp * DefaultMpRegenPercentPerSec /秒
    /// </summary>
    public void TickPlayerMpRegen(double dt, Dictionary<string, MapState> maps)
    {
        var alive = new HashSet<long>();
        foreach (var (mapName, map) in maps)
        {
            foreach (var (_, p) in map.Players)
            {
                alive.Add(p.AccountId);
                if (p.Mp >= p.MaxMp)
                {
                    _mpRegenAccum.Remove(p.AccountId);
                    continue;
                }

                // 判断是否在战斗中
                var ctx = _relations.Contexts.GetValueOrDefault(p.AccountId);
                bool inCombat = ctx != null && ctx.State == "COMBAT";

                // ---- 地形效果（战斗中/脱战都有）----
                int terrainMpDelta = GetTerrainMpRegen(p.AccountId, mapName, p.GridX, p.GridY);
                if (terrainMpDelta != 0)
                {
                    int terrainMpChange = (int)(terrainMpDelta * dt);
                    if (terrainMpChange != 0)
                    {
                        p.Mp = Math.Min(p.MaxMp, Math.Max(0, p.Mp + terrainMpChange));
                    }
                }

                double regenPerSec;
                if (p.MpRegen > 0)
                {
                    double multiplier = inCombat
                        ? GameConstants.CombatMpRegenMultiplier
                        : GameConstants.OutOfCombatMpRegenMultiplier;
                    regenPerSec = p.MpRegen * multiplier;
                }
                else
                {
                    regenPerSec = p.MaxMp * GameConstants.DefaultMpRegenPercentPerSec;
                }

                double accum = _mpRegenAccum.GetValueOrDefault(p.AccountId) + regenPerSec * dt;
                int regen = (int)accum;
                if (regen > 0)
                {
                    p.Mp = Math.Min(p.MaxMp, p.Mp + regen);
                    accum -= regen;
                }
                _mpRegenAccum[p.AccountId] = accum;
            }
        }

        // 清理已离线玩家的累积器
        foreach (var id in _mpRegenAccum.Keys.ToList())
        {
            if (!alive.Contains(id))
                _mpRegenAccum.Remove(id);
        }
    }

    // ---- 主 Tick ----

    public void Tick(double dt, Dictionary<string, MapState> maps, IMonsterRegistry? monsterRegistry)
    {
        // 处理死亡导致的怪物脱战
        if (_pendingDisengages.Count > 0 && monsterRegistry != null)
        {
            foreach (var monsterId in _pendingDisengages)
                monsterRegistry.OnDisengage(monsterId);
            _pendingDisengages.Clear();
        }

        var disengaged = _disengage.Tick(dt, _relations, maps);

        // 合并强制脱战请求（如怪物追击超时），统一走后续广播/通知流程
        while (_forceDisengagePairs.TryDequeue(out var pair))
        {
            var (a, b) = pair;
            if (_relations.HasActiveRelation(a, b))
            {
                _relations.RemoveBidirectionalRelation(a, b);
                disengaged.Add((0, a, b));
            }
        }

        if (disengaged.Count > 0)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var endLogs = new List<CombatLogEntry>();
            foreach (var (_, attackerId, targetId) in disengaged)
            {
                var combatId = _entityCombatId.GetValueOrDefault(attackerId);
                CombatTrace.Disengage(_logger, combatId, attackerId, SkillPipeline.GetEntityName(attackerId, maps), "timeout");
                CombatTrace.Disengage(_logger, combatId, targetId, SkillPipeline.GetEntityName(targetId, maps), "timeout");
                _entityCombatId.Remove(attackerId);
                _entityCombatId.Remove(targetId);
                string mapName = SkillPipeline.GetEntityMapName(attackerId, maps) ?? SkillPipeline.GetEntityMapName(targetId, maps) ?? "";
                string actorName = SkillPipeline.GetEntityName(attackerId, maps);
                string targetName = SkillPipeline.GetEntityName(targetId, maps);
                endLogs.Add(new()
                {
                    LogType = PGame.CombatLogType.CombatLogEnd,
                    Timestamp = (ulong)now,
                    ActorName = actorName, TargetName = targetName,
                    Extra = CombatLogFormatter.Format(7, actorName, targetName, null, 0, "战斗结束，已脱战"),
                    ActorId = attackerId,
                    MapName = mapName,
                });
            }
            BroadcastCombatLog(endLogs, maps);

            // 叙事触发：战斗结束
            foreach (var (_, attackerId, targetId) in disengaged)
            {
                string aName = SkillPipeline.GetEntityName(attackerId, maps);
                string tName = SkillPipeline.GetEntityName(targetId, maps);
                TryNarrate("combat_end", attackerId, targetId, aName, tName, 0, maps);
            }

            // 通知怪物脱战，恢复 AI 行为
            foreach (var (_, attackerId, targetId) in disengaged)
            {
                if (monsterRegistry != null)
                {
                    if (attackerId >= CombatConstants.MonsterIdThreshold) monsterRegistry.OnDisengage(attackerId);
                    if (targetId >= CombatConstants.MonsterIdThreshold) monsterRegistry.OnDisengage(targetId);
                }

                // 重置 NPC 的 InCombat 状态
                foreach (var map in maps.Values)
                {
                    if (attackerId < 1000000 && map.Npcs.TryGetValue(attackerId, out var npcA))
                        npcA.InCombat = false;
                    if (targetId < 1000000 && map.Npcs.TryGetValue(targetId, out var npcB))
                        npcB.InCombat = false;
                }
            }

            // 向脱战的实体发送 DisengageNotify + 向玩家发送 CombatEndNotify + 空 CombatStateNotify
            var notifiedPlayers = new HashSet<long>();
            foreach (var (_, attackerId, targetId) in disengaged)
            {
                foreach (var eid in new[] { attackerId, targetId })
                {
                    SendDisengageNotify(eid, maps);
                    if (eid < 1000000 && !notifiedPlayers.Contains(eid))
                    {
                        notifiedPlayers.Add(eid);
                        foreach (var map in maps.Values)
                        {
                            if (map.Players.TryGetValue(eid, out var p))
                            {
                                // 脱战清除应移除的 buff
                                p.Buffs.ClearOnDisengage();
                                ClearPreferredSkill(eid, maps);
                                var buffNotify = new PGame.BuffUpdateNotify { EntityId = (ulong)eid };
                                foreach (var b in p.Buffs.Buffs)
                                {
                                    var cfg = _tables?.GetBuff(b.BuffId);
                                    string bName = cfg?.Name ?? $"Buff{b.BuffId}";
                                    float remaining = b.ExpireTime <= 0 ? -1f : (float)(b.ExpireTime - Environment.TickCount64) / 1000f;
                                    if (remaining < 0 && b.ExpireTime > 0) remaining = 0;
                                    buffNotify.Buffs.Add(new PGame.BuffUpdateNotify.Types.BuffEntry
                                    {
                                        BuffId = b.BuffId, BuffName = bName, Stacks = b.Stacks,
                                        RemainingTime = remaining, ShieldAmount = b.ShieldRemaining,
                                    });
                                }
                                _network.SendToAccount(eid, p.ServerId,
                                    (int)PProtocol.MessageId.GameBuffUpdateNotify,
                                    buffNotify.ToByteArray());

                                p.InCombat = false; // 脱战
                                long otherId = attackerId == eid ? targetId : attackerId;
                                SendCombatEndNotify(eid, otherId, PGame.CombatEndReason.CombatEndDisengage, maps);
                                _network.SendToAccount(eid, p.ServerId,
                                    (int)PProtocol.MessageId.GameCombatStateNotify,
                                    new PGame.CombatStateNotify().ToByteArray());
                                break;
                            }
                        }
                    }
                }
            }
        }

        // 同步玩家 PreferredSkillId 到 CombatContext
        SyncPreferredSkills(maps);

        TickMonsterSkills(dt, maps);
        TickMonsterRegen(dt, monsterRegistry);
        TickPlayerHpRegen(dt, maps);
        TickPlayerMpRegen(dt, maps);

        // Buff 过期检查 + DOT tick
        TickBuffs(dt, maps);

        // 脱战玩家的 HP/MP 变化推送
        SyncOutOfCombatHpMp(maps);
    }

    /// <summary>
    /// 脱战玩家的 HP/MP 变化推送（仅在数值变化时发送）
    /// 战斗中的玩家由 BroadcastCombatState 推送，脱战玩家需要单独推送
    /// </summary>
    private void SyncOutOfCombatHpMp(Dictionary<string, MapState> maps)
    {
        var alive = new HashSet<long>();
        foreach (var map in maps.Values)
        {
            foreach (var (accountId, p) in map.Players)
            {
                alive.Add(accountId);

                // 战斗中的玩家由 BroadcastCombatState 处理
                var ctx = _relations.Contexts.GetValueOrDefault(accountId);
                if (ctx != null && ctx.State == "COMBAT")
                {
                    _lastSyncedHpMp[accountId] = (p.Hp, p.Mp);
                    continue;
                }

                // 只在 HP/MP 变化时才推送
                if (_lastSyncedHpMp.TryGetValue(accountId, out var last) && last.hp == p.Hp && last.mp == p.Mp)
                    continue;

                _lastSyncedHpMp[accountId] = (p.Hp, p.Mp);

                var notify = new PGame.CombatStateNotify();
                var unit = new PGame.CombatStateNotify.Types.CombatUnit
                {
                    EntityId = (ulong)accountId,
                    EntityName = p.RoleName ?? $"player_{accountId}",
                    Atb = 0,
                    IsPlayer = true,
                    Hp = p.Hp,
                    MaxHp = p.MaxHp,
                    Mp = p.Mp,
                    MaxMp = p.MaxMp,
                };
                notify.Units.Add(unit);
                _network.SendToAccount(accountId, p.ServerId,
                    (int)PProtocol.MessageId.GameCombatStateNotify, notify.ToByteArray());
            }
        }

        // 清理离线玩家
        foreach (var id in _lastSyncedHpMp.Keys.ToList())
        {
            if (!alive.Contains(id))
                _lastSyncedHpMp.Remove(id);
        }
    }

    // ---- 辅助 ----

    /// <summary>尝试触发叙事文本并广播</summary>
    private void TryNarrate(string condition, long actorId, long targetId, string actorName, string targetName, double thresholdValue, Dictionary<string, MapState> maps)
    {
        if (_narration == null) return;
        var text = _narration.TryTrigger(condition, actorId, targetId, actorName, targetName, thresholdValue);
        if (text == null) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        BroadcastCombatLog(new List<CombatLogEntry>
        {
            new()
            {
                LogType = PGame.CombatLogType.CombatLogBuff, // 复用 Buff 类型（紫色）
                Timestamp = (ulong)now,
                ActorName = actorName, TargetName = targetName,
                Extra = text,
                ActorId = actorId,
                MapName = SkillPipeline.GetEntityMapName(actorId, maps) ?? SkillPipeline.GetEntityMapName(targetId, maps) ?? "",
            }
        }, maps);
    }

    private int SelectSkill(CombatContext ctx, Dictionary<string, MapState> maps)
    {
        long now = Environment.TickCount64;
        long entityId = ctx.EntityId;

        // 辅助：检查技能是否有目标在施法范围内
        bool IsInRange(int skillId)
        {
            var cfg = SkillPipeline.GetSkillConfigStatic(skillId);
            if (cfg == null) return false;
            if (cfg.TargetType == ESkillTargetType.Self) return true;

            var casterPositions = SkillPipeline.FindEntityCombatPositions(entityId, maps);
            if (casterPositions.Count == 0) return false;

            foreach (var relationId in ctx.RelationIds)
            {
                var rel = _relations.Relations.GetValueOrDefault(relationId);
                if (rel == null || !rel.IsActive) continue;

                long targetId = 0;
                if (cfg.TargetType == ESkillTargetType.SingleEnemy || cfg.TargetType == ESkillTargetType.AllEnemiesInRange)
                {
                    targetId = rel.AttackerId == entityId ? rel.TargetId :
                               rel.TargetId == entityId ? rel.AttackerId : 0;
                }
                else if (cfg.TargetType == ESkillTargetType.AllAlliesInRange)
                {
                    bool casterIsPlayer = entityId < CombatConstants.MonsterIdThreshold;
                    long otherId = rel.AttackerId == entityId ? rel.TargetId : rel.AttackerId;
                    bool otherIsPlayer = otherId < CombatConstants.MonsterIdThreshold;
                    if (casterIsPlayer == otherIsPlayer)
                        targetId = otherId;
                }

                if (targetId == 0) continue;
                var targetPositions = SkillPipeline.FindEntityCombatPositions(targetId, maps);
                if (targetPositions.Count == 0) continue;
                int d = SkillPipeline.MinCombatDistance(casterPositions, targetPositions);
                if (d <= cfg.CastRange) return true;
            }
            return false;
        }

        // 优先释放被选中的技能
        if (ctx.PreferredSkillId > 0 &&
            ctx.SkillPool.Contains(ctx.PreferredSkillId) &&
            (!ctx.SkillCooldowns.TryGetValue(ctx.PreferredSkillId, out var preferredCdEnd) || now >= preferredCdEnd) &&
            IsInRange(ctx.PreferredSkillId))
        {
            return ctx.PreferredSkillId;
        }

        // 若已选定一次性技能但尚未就绪/不在射程，保持选定状态，本 tick 不回落到常规 AI 选择
        if (ctx.PreferredSkillId > 0 && ctx.SkillPool.Contains(ctx.PreferredSkillId))
            return 0;

        // 常规顺序选择（CD 就绪 + 目标在范围内）
        foreach (var skillId in ctx.SkillPool)
        {
            if (!ctx.SkillCooldowns.TryGetValue(skillId, out var cdEnd) || now >= cdEnd)
            {
                if (IsInRange(skillId))
                    return skillId;
            }
        }
        return 0; // 所有技能冷却中或目标不在范围内
    }

    /// <summary>记录 SelectSkill 决策（供调试）</summary>
    private int SelectSkillWithLog(CombatContext ctx, long entityId, Dictionary<string, MapState> maps)
    {
        int skillId = SelectSkill(ctx, maps);
        if (skillId == 0)
        {
            _logger.LogDebug("[Combat] SelectSkill returned 0: entity={EntityId} skillPool=[{Pool}] cooldowns=[{Cds}]",
                entityId, string.Join(",", ctx.SkillPool),
                string.Join(",", ctx.SkillCooldowns.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        else
        {
            _logger.LogDebug("[Combat] SelectSkill chose: entity={EntityId} skill={SkillId}", entityId, skillId);
        }
        return skillId;
    }

    private void SetCombatJob(CombatContext ctx, long entityId, Dictionary<string, MapState> maps)
    {
        if (ctx.Job != "")
        {
            _logger.LogDebug("[Combat] SetCombatJob skipped: entity={EntityId} job already set to '{Job}'", entityId, ctx.Job);
            return;
        }
        foreach (var map in maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
            {
                ctx.Job = p.Job ?? "";
                ctx.SkillPool = new List<int>(p.EquippedSkills.Where(s => s > 0));
                ctx.PreferredSkillId = p.PreferredSkillId;
                _logger.LogInformation("[Combat] SetCombatJob player: entity={EntityId} job={Job} skills=[{Skills}] preferred={Preferred}",
                    entityId, ctx.Job, string.Join(",", ctx.SkillPool), ctx.PreferredSkillId);
                return;
            }
        }

        // 怪物：从 Luban 配置表读取技能池
        ctx.Job = "monster";
        if (_tables != null)
        {
            foreach (var map in maps.Values)
            {
                if (map.Monsters.TryGetValue(entityId, out var m))
                {
                    var skills = _tables.GetMonsterSkills(m.MonsterId);
                    _logger.LogInformation("[Combat] SetCombatJob monster lookup: entity={EntityId} monsterId={MonsterId} skills=[{Skills}] count={Count}",
                        entityId, m.MonsterId, string.Join(",", skills), skills.Count);
                    if (skills.Count > 0)
                    {
                        ctx.SkillPool = skills;
                        _logger.LogInformation("[Combat] SetCombatJob monster: entity={EntityId} monsterId={MonsterId} skillPool=[{Skills}]",
                            entityId, m.MonsterId, string.Join(",", ctx.SkillPool));
                        return;
                    }
                }
            }
        }

        // 兜底：无配置时使用第一个可用技能
        ctx.SkillPool = new List<int>();
        _logger.LogWarning("[Combat] SetCombatJob fallback: entity={EntityId} job={Job} skillPool is empty", entityId, ctx.Job);
    }

    private void SendCombatStartNotify(long entityA, long entityB, Dictionary<string, MapState> maps)
    {
        var notify = new PGame.CombatStartNotify();
        notify.EntityIds.Add((ulong)entityA);
        notify.EntityIds.Add((ulong)entityB);
        foreach (var id in new[] { entityA, entityB })
        {
            if (id > 0)
            {
                var mapName = SkillPipeline.GetEntityMapName(id, maps);
                if (mapName == null) continue;
                var p = maps[mapName].Players.GetValueOrDefault(id);
                if (p != null)
                    _network.SendToAccount(id, p.ServerId, (int)PProtocol.MessageId.GameCombatStartNotify, notify.ToByteArray());
            }
        }
    }

    private void SendCombatEndNotify(long playerId, long otherEntityId, PGame.CombatEndReason reason, Dictionary<string, MapState> maps)
    {
        var notify = new PGame.CombatEndNotify { Reason = reason };
        notify.EntityIds.Add((ulong)playerId);
        notify.EntityIds.Add((ulong)otherEntityId);
        var mapName = SkillPipeline.GetEntityMapName(playerId, maps);
        if (mapName == null) return;
        var p = maps[mapName].Players.GetValueOrDefault(playerId);
        if (p != null)
            _network.SendToAccount(playerId, p.ServerId, (int)PProtocol.MessageId.GameCombatEndNotify, notify.ToByteArray());
    }

    // ---- 新网络消息广播 ----

    public void BroadcastCastStartNotify(long casterId, int skillId, float castTime, long targetId, int targetX, int targetY, Dictionary<string, MapState> maps)
    {
        var notify = new PGame.CastStartNotify
        {
            CasterId = (ulong)casterId,
            SkillId = (uint)skillId,
            CastTime = castTime,
            TargetId = (ulong)targetId,
            TargetX = targetX,
            TargetY = targetY,
        };
        string mapName = SkillPipeline.GetEntityMapName(casterId, maps) ?? "";
        if (maps.TryGetValue(mapName, out var map))
        {
            var data = notify.ToByteArray();
            foreach (var (accountId, p) in map.Players)
                _network.SendToAccount(accountId, p.ServerId, (int)PProtocol.MessageId.GameCastStartNotify, data);
        }
    }

    public void BroadcastCastResultNotify(long casterId, int skillId, List<long> targetIds, bool isMiss, Dictionary<string, MapState> maps)
    {
        var notify = new PGame.CastResultNotify
        {
            CasterId = (ulong)casterId,
            SkillId = (uint)skillId,
            IsMiss = isMiss,
        };
        foreach (var t in targetIds) notify.TargetIds.Add((ulong)t);

        string mapName = SkillPipeline.GetEntityMapName(casterId, maps) ?? "";
        if (maps.TryGetValue(mapName, out var map))
        {
            var data = notify.ToByteArray();
            foreach (var (accountId, p) in map.Players)
                _network.SendToAccount(accountId, p.ServerId, (int)PProtocol.MessageId.GameCastResultNotify, data);
        }
    }

    public void BroadcastProjectileSpawnNotify(long projectileId, long casterId, int skillId,
        int fromX, int fromY, int toX, int toY, float speed, Dictionary<string, MapState> maps)
    {
        var notify = new PGame.ProjectileSpawnNotify
        {
            ProjectileId = (ulong)projectileId,
            SkillId = (uint)skillId,
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            Speed = speed,
        };
        // 广播给施法者所在地图上的所有玩家
        string mapName = SkillPipeline.GetEntityMapName(casterId, maps) ?? "";
        if (maps.TryGetValue(mapName, out var map))
        {
            var data = notify.ToByteArray();
            foreach (var (accountId, p) in map.Players)
                _network.SendToAccount(accountId, p.ServerId, (int)PProtocol.MessageId.GameProjectileSpawnNotify, data);
        }
    }

    public void BroadcastProjectileHitNotify(long projectileId, long targetId, int skillId, Dictionary<string, MapState> maps)
    {
        var notify = new PGame.ProjectileHitNotify
        {
            ProjectileId = (ulong)projectileId,
            TargetId = (ulong)targetId,
        };
        string mapName = SkillPipeline.GetEntityMapName(targetId, maps) ?? "";
        if (maps.TryGetValue(mapName, out var map))
        {
            var data = notify.ToByteArray();
            foreach (var (accountId, p) in map.Players)
                _network.SendToAccount(accountId, p.ServerId, (int)PProtocol.MessageId.GameProjectileHitNotify, data);
        }
    }

    public void BroadcastCombatEventNotify(long targetId, int hpDelta, int mpDelta, Dictionary<string, MapState> maps)
    {
        var notify = new PGame.CombatEventNotify
        {
            TargetId = (ulong)targetId,
            HpDelta = hpDelta,
            MpDelta = mpDelta,
        };
        string mapName = SkillPipeline.GetEntityMapName(targetId, maps) ?? "";
        if (maps.TryGetValue(mapName, out var map))
        {
            var data = notify.ToByteArray();
            foreach (var (accountId, p) in map.Players)
                _network.SendToAccount(accountId, p.ServerId, (int)PProtocol.MessageId.GameCombatEventNotify, data);
        }
    }

    public void SendDisengageNotify(long entityId, Dictionary<string, MapState> maps)
    {
        var notify = new PGame.DisengageNotify { EntityId = (ulong)entityId };
        string mapName = SkillPipeline.GetEntityMapName(entityId, maps) ?? "";
        if (!maps.TryGetValue(mapName, out var map)) return;

        if (entityId < CombatConstants.MonsterIdThreshold)
        {
            var p = map.Players.GetValueOrDefault(entityId);
            if (p != null)
                _network.SendToAccount(entityId, p.ServerId, (int)PProtocol.MessageId.GameDisengageNotify, notify.ToByteArray());
        }
        else
        {
            var data = notify.ToByteArray();
            foreach (var (accountId, p) in map.Players)
                _network.SendToAccount(accountId, p.ServerId, (int)PProtocol.MessageId.GameDisengageNotify, data);
        }
    }

    private void BroadcastCombatState(Dictionary<string, MapState> maps)
    {
        long nowMs = Environment.TickCount64;
        var playerStates = new Dictionary<long, List<(long id, string name, double _, bool isPlayer, int hp, int maxHp, int mp, int maxMp, string castingSkill, float castProgress, List<(uint skillId, float remainingCd, float totalCd)> cds)>>();

        foreach (var (mapName, map) in maps)
        {
            foreach (var (accountId, p) in map.Players)
            {
                var ctx = _relations.Contexts.GetValueOrDefault(accountId);
                if (ctx == null || ctx.State != "COMBAT") continue;

                var (cs, cp) = GetCastingInfo(ctx);
                var playerCds = BuildCdEntries(ctx, nowMs);
                var unitSet = new HashSet<long> { accountId };
                var units = new List<(long, string, double, bool, int, int, int, int, string, float, List<(uint, float, float)>)>
                {
                    (accountId, p.RoleName ?? $"player_{accountId}", 0d, true, p.Hp, p.MaxHp, p.Mp, p.MaxMp, cs, cp, playerCds)
                };

                foreach (var relationId in ctx.RelationIds)
                {
                    var rel = _relations.Relations.GetValueOrDefault(relationId);
                    if (rel == null || !rel.IsActive) continue;
                    long otherId = rel.AttackerId == accountId ? rel.TargetId : rel.AttackerId;
                    if (!unitSet.Add(otherId)) continue;

                    var otherCtx = _relations.Contexts.GetValueOrDefault(otherId);
                    int otherHp = 0, otherMaxHp = 0, otherMp = 0, otherMaxMp = 0;
                    foreach (var m2 in maps.Values)
                    {
                        if (m2.Players.TryGetValue(otherId, out var op2)) { otherHp = op2.Hp; otherMaxHp = op2.MaxHp; otherMp = op2.Mp; otherMaxMp = op2.MaxMp; break; }
                        if (m2.Monsters.TryGetValue(otherId, out var om2)) { otherHp = om2.Hp; otherMaxHp = om2.MaxHp; otherMp = om2.Mp; otherMaxMp = om2.MaxMp; break; }
                        if (m2.Npcs.TryGetValue(otherId, out var on2)) { otherHp = on2.Hp; otherMaxHp = on2.MaxHp; otherMp = on2.Mp; otherMaxMp = on2.MaxMp; break; }
                    }
                    var (ocs, ocp) = GetCastingInfo(otherCtx);
                    var otherCds = BuildCdEntries(otherCtx, nowMs);

                    units.Add((otherId, SkillPipeline.GetEntityName(otherId, maps),
                        0d, otherId < 1000000, otherHp, otherMaxHp, otherMp, otherMaxMp, ocs, ocp, otherCds));
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
            foreach (var (id, name, _, isPlayer, hp, maxHp, mp, maxMp, castingSkill, castProgress, cds) in units)
            {
                var unit = new PGame.CombatStateNotify.Types.CombatUnit
                {
                    EntityId = (ulong)id, EntityName = name, Atb = 0f,
                    IsPlayer = isPlayer, Hp = hp, MaxHp = maxHp, Mp = mp, MaxMp = maxMp,
                    CastingSkill = castingSkill, CastProgress = castProgress,
                };
                foreach (var (skillId, remainingCd, totalCd) in cds)
                    unit.SkillCds.Add(new PGame.CombatStateNotify.Types.SkillCdEntry
                        { SkillId = skillId, RemainingCd = remainingCd, TotalCd = totalCd });
                notify.Units.Add(unit);
            }

            _network.SendToAccount(accountId, serverId, (int)PProtocol.MessageId.GameCombatStateNotify, notify.ToByteArray());
        }
    }

    private static List<(uint skillId, float remainingCd, float totalCd)> BuildCdEntries(CombatContext? ctx, long nowMs)
    {
        var result = new List<(uint, float, float)>();
        if (ctx?.SkillCooldowns == null) return result;
        foreach (var (skillId, cdEnd) in ctx.SkillCooldowns)
        {
            float remaining = Math.Max(0f, (cdEnd - nowMs) / 1000f);
            float total = (float)(SkillPipeline.GetSkillConfigStatic(skillId)?.Cooldown ?? 0);
            if (remaining > 0)
                result.Add(((uint)skillId, remaining, total));
        }
        return result;
    }

    private static (string skill, float progress) GetCastingInfo(CombatContext? ctx)
    {
        if (ctx == null || ctx.SubState != "CASTING" || ctx.CastSkillId == null || ctx.CastEndTime == null)
            return ("", 0f);

        var cfg = SkillPipeline.GetSkillConfigStatic(ctx.CastSkillId.Value);
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

    /// <summary>
    /// 从地图状态中查找玩家的 BuffContainer，用于战斗上下文共享
    /// </summary>
    private static BuffContainer? FindPlayerBuffs(long entityId, Dictionary<string, MapState> maps)
    {
        foreach (var (_, map) in maps)
        {
            if (map.Players.TryGetValue(entityId, out var player))
                return player.Buffs;
        }
        return null; // 怪物/NPC 没有 MapPlayerState，返回 null 让 CombatRelationManager 创建新的
    }

    /// <summary>设置 MapPlayerState.InCombat 标志</summary>
    private static void SetPlayerInCombat(long entityId, bool inCombat, Dictionary<string, MapState> maps)
    {
        foreach (var map in maps.Values)
        {
            if (map.Players.TryGetValue(entityId, out var p))
            {
                p.InCombat = inCombat;
                return;
            }
        }
    }

    // ---- Buff Tick: 过期检查 + DOT tick + 变化推送 ----

    /// <summary>
    /// 每帧检查所有战斗中实体的 Buff：
    /// 1. 过期移除
    /// 2. DOT tick（中毒等）
    /// 3. buff 数量变化时推送 BuffUpdateNotify
    /// </summary>
    private void TickBuffs(double dt, Dictionary<string, MapState> maps)
    {
        if (_tables == null) return;
        var now = Environment.TickCount64;
        var changedPlayers = new HashSet<long>(); // buff 列表变化的玩家
        var dotResults = new List<(long TargetId, int Damage, string DamageType, long CasterId)>();

        foreach (var (entityId, ctx) in _relations.Contexts)
        {
            var buffs = ctx.Buffs;
            if (buffs == null) continue;

            int prevCount = buffs.Buffs.Count;
            bool expired = false;

            for (int i = buffs.Buffs.Count - 1; i >= 0; i--)
            {
                var buff = buffs.Buffs[i];

                // 过期检查
                if (buff.ExpireTime > 0 && now >= buff.ExpireTime)
                {
                    CombatTrace.BuffExpire(_logger, _entityCombatId.GetValueOrDefault(entityId), entityId, SkillPipeline.GetEntityName(entityId, maps), buff.BuffId, _tables?.GetBuff(buff.BuffId)?.Name ?? $"Buff{buff.BuffId}");
                    buffs.RemoveBuff(buff.BuffId);
                    expired = true;
                    continue;
                }

                // DOT tick
                var cfg = _tables?.GetBuff(buff.BuffId);
                if (cfg != null && cfg.TickInterval > 0 && cfg.Tags.Contains("dot"))
                {
                    if (buff.LastTickTime == 0) buff.LastTickTime = buff.ApplyTime;
                    long elapsed = now - buff.LastTickTime;
                    long intervalMs = (long)(cfg.TickInterval * 1000);
                    if (elapsed >= intervalMs)
                    {
                        foreach (var effect in cfg.Effects)
                        {
                            if (effect.Trigger != "OnTick") continue;
                            // 魔法 DOT 用 SnapshotMatk，物理 DOT 用 SnapshotAtk
                            int baseAtk = effect.DamageType == 2 ? buff.SnapshotMatk : buff.SnapshotAtk;
                            int dmg = (int)(baseAtk * effect.Coefficient);
                            string dmgType = effect.DamageType == 2 ? "magical" : "physical";
                            if (dmg > 0)
                            {
                                CombatTrace.BuffTick(_logger, _entityCombatId.GetValueOrDefault(entityId), entityId, SkillPipeline.GetEntityName(entityId, maps), buff.BuffId, cfg?.Name ?? $"Buff{buff.BuffId}", dmg, dmgType, buff.TickCount);
                                dotResults.Add((entityId, dmg, dmgType, buff.CasterId));
                            }
                        }
                        buff.LastTickTime = now;
                        buff.TickCount++;
                    }
                }
            }

            // 检测 buff 数量变化（过期或技能新增）
            // 也检测护盾消耗变化
            int curCount = buffs.Buffs.Count;
            int curShield = buffs.GetShieldAmount();
            _lastBuffCount.TryGetValue(entityId, out int lastCount);
            _lastShieldAmount.TryGetValue(entityId, out int lastShield);
            bool shieldChanged = curShield != lastShield;
            if (expired || curCount != lastCount || shieldChanged)
            {
                _lastBuffCount[entityId] = curCount;
                _lastShieldAmount[entityId] = curShield;
                if (entityId < 1000000) // 只给玩家推送
                    changedPlayers.Add(entityId);
            }
        }

        // 推送 buff 变化通知
        foreach (var playerId in changedPlayers)
        {
            foreach (var map in maps.Values)
            {
                if (map.Players.TryGetValue(playerId, out var p))
                {
                    var notify = new PGame.BuffUpdateNotify { EntityId = (ulong)playerId };
                    foreach (var b in p.Buffs.Buffs)
                    {
                        var cfg = _tables?.GetBuff(b.BuffId);
                        string bName = cfg?.Name ?? $"Buff{b.BuffId}";
                        float remaining = b.ExpireTime <= 0 ? -1f : (float)(b.ExpireTime - now) / 1000f;
                        if (remaining < 0 && b.ExpireTime > 0) remaining = 0;
                        notify.Buffs.Add(new PGame.BuffUpdateNotify.Types.BuffEntry
                        {
                            BuffId = b.BuffId, BuffName = bName, Stacks = b.Stacks,
                            RemainingTime = remaining, ShieldAmount = b.ShieldRemaining,
                        });
                    }
                    _network.SendToAccount(playerId, p.ServerId,
                        (int)PProtocol.MessageId.GameBuffUpdateNotify, notify.ToByteArray());
                    break;
                }
            }
        }

        // 更新弹道
        var projectileHits = _projectileMgr.Tick(dt, maps, _relations);
        foreach (var hit in projectileHits)
        {
            var (damage, dtype) = DealDamageAction.CalcDamage(hit.CasterId, hit.TargetId, hit.DamageType, hit.Coefficient, maps, this);
            ApplyDamage(hit.CasterId, hit.TargetId, damage, dtype, maps);
            BroadcastProjectileHitNotify(hit.ProjectileId, hit.TargetId, hit.SkillId, maps);
        }

        // 应用 DOT 伤害
        foreach (var (targetId, damage, damageType, casterId) in dotResults)
        {
            ApplyDamage(casterId, targetId, damage, damageType, maps);
        }
    }
}
