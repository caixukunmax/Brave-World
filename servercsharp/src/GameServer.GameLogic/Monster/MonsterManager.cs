using System.Collections.Concurrent;
using GameServer.Common;
using GameServer.Common.Buffs;
using GameServer.Common.Config;
using GameServer.Services.Core;
using GameServer.Services.Map;
using GameServer.Services.Map.Combat;
using GameServer.Services.Monster.AI;
using GameServer.Services.World;
using GameServer.Tables;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Monster;

/// <summary>
/// 怪物管理器 — 合并 MonsterService + MonsterLogic
/// 管理怪物状态、AI tick、初始化、战斗回调
/// </summary>
public class MonsterManager : IMonsterRegistry
{
    private readonly ILogger<MonsterManager> _logger;
    private readonly MapDataProvider _mapData;
    private readonly MapService _mapService;
    private readonly Core.INetworkSender _network;
    private readonly LubanTableLoader _tables;
    private readonly Dictionary<string, IBehaviorHandler> _handlers;
    private readonly Dictionary<CombatBehaviorType, ICombatBehaviorHandler> _combatBehaviors;

    /// <summary>怪物死亡回调：(monsterInstanceId, attackerId, monsterId, mapName, x, y) => void</summary>
    public Action<long, long, int, string, int, int>? OnMonsterDeath { get; set; }

    /// <summary>战斗管理器引用（用于追击超时后断开关系）</summary>
    public CombatManager? CombatManager { get; set; }

    private readonly ConcurrentDictionary<long, MonsterRuntimeState> _monsters = new();
    private readonly List<RespawnEntry> _respawnEntries = new();

    public MonsterManager(
        ILogger<MonsterManager> logger,
        MapDataProvider mapData,
        MapService mapService,
        Core.INetworkSender network,
        LubanTableLoader tables)
    {
        _logger = logger;
        _mapData = mapData;
        _mapService = mapService;
        _network = network;
        _tables = tables;
        _combatBehaviors = new Dictionary<CombatBehaviorType, ICombatBehaviorHandler>
        {
            [CombatBehaviorType.Melee] = new CombatChaseBehavior(mapData),
            [CombatBehaviorType.Ranged] = new RangedCombatBehavior(mapData),
            [CombatBehaviorType.Caster] = new CasterCombatBehavior(mapData),
        };

        _handlers = new Dictionary<string, IBehaviorHandler>
        {
            ["patrol"] = new PatrolBehavior(mapData),
            ["patrol_chase"] = new PatrolChaseBehavior(mapData),
            ["guard"] = new PatrolChaseBehavior(mapData),
        };
    }

    public void Init()
    {
        _logger.LogInformation("[Monster] initializing...");
        _monsters.Clear();

        int nextId = (int)CombatConstants.MonsterIdThreshold + 1;
        foreach (var (mapName, _) in _mapData.GetAllMaps())
        {
            var mapMonsters = InitMonstersForMap(mapName, nextId);
            foreach (var (instanceId, m) in mapMonsters)
            {
                _monsters[instanceId] = m;
                _mapService.MonsterEnter(instanceId, m.MonsterId, mapName, m.Name, m.X, m.Y, m.Hp, m.MaxHp, m.Level,
                    m.Patk, m.Matk, m.Pdef, m.Mdef, m.SizeX, m.SizeY);
                BindMonsterToMapState(m);
                _logger.LogInformation("[Monster] init: id={InstanceId} map={Map} pos=({X},{Y}) ai={Ai}", instanceId, mapName, m.X, m.Y, m.AiType);
            }
            nextId += mapMonsters.Count;
        }

        _logger.LogInformation("[Monster] total={Count} across {Maps} maps", _monsters.Count, _mapData.GetAllMaps().Count);
    }

    /// <summary>复活队列条目</summary>
    private class RespawnEntry
    {
        public long InstanceId { get; set; }
        public int MonsterId { get; set; }
        public string MapName { get; set; } = "";
        public int SpawnX { get; set; }
        public int SpawnY { get; set; }
        public int SizeX { get; set; } = 1;
        public int SizeY { get; set; } = 1;
        public int DeathX { get; set; }
        public int DeathY { get; set; }
        public int RespawnTimeSec { get; set; }
        public long DeathTimeMs { get; set; }
        public ERespawnType RespawnType { get; set; }
        public int RespawnRange { get; set; }
        public string Name { get; set; } = "";
        public int Level { get; set; }
        public int MaxHp { get; set; }
        public int Patk { get; set; }
        public int Matk { get; set; }
        public int Pdef { get; set; }
        public int Mdef { get; set; }
        public int MoveSpeedMs { get; set; }
        public string AiType { get; set; } = "patrol";
        public AiConfig AiConfig { get; set; } = new();
    }

    public void Tick()
    {
        try
        {
            // 按地图分组处理：直接遍历 ConcurrentDictionary（线程安全快照枚举），
            // 避免 ToArray + GroupBy 的额外分配
            var byMap = new Dictionary<string, List<KeyValuePair<long, MonsterRuntimeState>>>();
            foreach (var kv in _monsters)
            {
                if (!byMap.TryGetValue(kv.Value.MapName, out var list))
                {
                    list = new List<KeyValuePair<long, MonsterRuntimeState>>();
                    byMap[kv.Value.MapName] = list;
                }
                list.Add(kv);
            }

            foreach (var (mapName, monsters) in byMap)
            {
                var players = GetOnlinePlayers(mapName);
                // 高频 tick 不再打印玩家坐标，避免刷屏；如需调试可用日志级别开关或断点
                var movedMonsters = new List<(long id, int fx, int fy, int tx, int ty, MonsterState state, int durationMs)>();
                var cancelledMonsters = new List<(long id, int rollbackX, int rollbackY)>();

                foreach (var (instanceId, m) in monsters)
                {
                    bool inReturnCooldown = m.ReturnCooldownEndMs > Environment.TickCount64;
                    UpdateTerritoryNarration(m, players);

                    // ---- 1. 处理正在移动的怪物 ----
                    if (m.IsMoving)
                    {
                        long elapsed = Environment.TickCount64 - m.MoveStartTime;
                        int moveDuration = m.MoveSpeedMs > 0 ? m.MoveSpeedMs : GameConstants.DefaultMonsterMoveSpeedMs;

                        // 检查点确认（与玩家一致的机制）
                        if (!m.CheckpointConfirmed && elapsed >= moveDuration * GameConstants.MoveCheckRatio / 100)
                        {
                            var res = _mapService.World.GetReservation(instanceId);
                            if (res != null && res.CollisionPending)
                            {
                                // 碰撞性移动：30% 检查点检测敌人
                                var result = _mapService.World.ConfirmMoveEx(instanceId);
                                m.IsMoving = false;
                                m.CheckpointConfirmed = false;
                                cancelledMonsters.Add((instanceId, m.X, m.Y));
                                if (result == ConfirmResult.Collision)
                                {
                                    if (!inReturnCooldown)
                                    {
                                        _mapService.CheckEntityCollision(instanceId, mapName, m.X, m.Y);
                                    }
                                }
                                continue;
                            }

                            bool ok = _mapService.World.ConfirmMove(instanceId);
                            if (!ok)
                            {
                                // 目标格被占，取消移动并回滚
                                _mapService.World.CancelMove(instanceId);
                                m.IsMoving = false;
                                m.CheckpointConfirmed = false;
                                cancelledMonsters.Add((instanceId, m.X, m.Y));
                                if (!inReturnCooldown)
                                {
                                    _mapService.CheckEntityCollision(instanceId, mapName, m.X, m.Y);
                                }
                                continue;
                            }
                            m.CheckpointConfirmed = true;
                        }

                        // 移动完成
                        if (elapsed >= moveDuration)
                        {
                            _mapService.World.CompleteMove(instanceId);
                            m.Direction = DirectionUtil.FromMoveVector(m.X, m.Y, m.MoveTargetX, m.MoveTargetY);
                            m.X = m.MoveTargetX;
                            m.Y = m.MoveTargetY;
                            m.IsMoving = false;
                            m.CheckpointConfirmed = false;

                            // 到达新格后，统一碰撞检测（与玩家一致）
                            // 回归冷却期内不触发碰撞，避免回归后立即重新开战
                            if (!inReturnCooldown)
                            {
                                _mapService.CheckEntityCollision(instanceId, mapName, m.X, m.Y);
                            }
                        }
                        continue; // 移动中不执行 AI
                    }

                    // ---- 2. 追击超时检查（领地外） ----
                    if (m.InCombat && m.TargetId.HasValue)
                    {
                        CheckChaseTimeout(m, players);
                    }

                    // 回归状态或回归冷却期内，强制使用 Overworld 行为（巡逻/回归）
                    MonsterAiMode effectiveMode = (m.State is MonsterState.Return or MonsterState.ForcedReturn || inReturnCooldown) ? MonsterAiMode.Overworld : m.Mode;

                    // ---- 3. AI 决策 ----
                    var next = effectiveMode == MonsterAiMode.Combat
                        ? RunCombatBehavior(m, mapName, players)
                        : RunOverworldBehavior(m, mapName, players);
                    if (next == null) continue;

                    var (nx, ny) = next.Value;
                    if (nx == m.X && ny == m.Y) continue;

                    // ---- 3. 战斗中怪物：0% 碰撞判断 ----
                    // 已在战斗的怪物追击时，目标格有敌人则直接触发碰撞，
                    // 不走移动→30%→弹回的流程，避免视觉抖动
                    if (m.InCombat && _mapService.World.HasEnemyAt(mapName, nx, ny, instanceId))
                    {
                        _mapService.CheckEntityCollision(instanceId, mapName, m.X, m.Y);
                        continue;
                    }

                    // ---- 4. 预占目标格 ----
                    double speedMultiplier = ((m.State is MonsterState.Return or MonsterState.ForcedReturn) && m.AiConfig.ReturnSpeedMultiplier is > 0)
                        ? m.AiConfig.ReturnSpeedMultiplier.Value
                        : 1.0;
                    int baseSpeedMs = m.MoveSpeedMs > 0 ? m.MoveSpeedMs : GameConstants.DefaultMonsterMoveSpeedMs;
                    int durationMs = (int)(baseSpeedMs / speedMultiplier);
                    bool reserved = _mapService.World.TryReserveMove(
                        instanceId, mapName, m.X, m.Y, nx, ny,
                        durationMs, GameConstants.MoveCheckRatio,
                        GameConstants.MoveDualGridStartRatio, GameConstants.MoveDualGridEndRatio);

                    if (reserved)
                    {
                        // 只预占，不立即确认坐标。动画完成后才 ConfirmMove + 碰撞检测（与玩家一致）
                        m.IsMoving = true;
                        m.MoveTargetX = nx;
                        m.MoveTargetY = ny;
                        m.MoveStartTime = Environment.TickCount64;

                        movedMonsters.Add((instanceId, m.X, m.Y, nx, ny, m.State, durationMs));
                    }
                    else
                    {
                        // 目标格被占据 — 尝试碰撞性移动（移动 30% 后再检测）
                        bool collisionMove = _mapService.World.TryReserveCollisionMove(
                            instanceId, mapName, m.X, m.Y, nx, ny,
                            durationMs, GameConstants.MoveCheckRatio,
                            GameConstants.MoveDualGridStartRatio, GameConstants.MoveDualGridEndRatio);

                        if (collisionMove)
                        {
                            m.IsMoving = true;
                            m.MoveTargetX = nx;
                            m.MoveTargetY = ny;
                            m.MoveStartTime = Environment.TickCount64;
                            movedMonsters.Add((instanceId, m.X, m.Y, nx, ny, m.State, durationMs));
                        }
                    }
                }

                // 按地图广播移动通知
                foreach (var (id, fx, fy, tx, ty, state, durationMs) in movedMonsters)
                {
                    var notify = new PGame.MonsterMoveNotify
                    {
                        InstanceId = (uint)id,
                        FromX = fx,
                        FromY = fy,
                        ToX = tx,
                        ToY = ty,
                        State = state.ToStateString(),
                        DurationMs = durationMs,
                        Direction = DirectionUtil.FromMoveVector(fx, fy, tx, ty),
                    };
                    _mapService.BroadcastToMap(mapName, (int)PProtocol.MessageId.GameMonsterMoveNotify, notify.ToByteArray());
                }

                // 广播取消通知（检查点目标格被占）
                foreach (var (id, rollbackX, rollbackY) in cancelledMonsters)
                {
                    var cancelNotify = new PGame.MonsterMoveCancelNotify
                    {
                        InstanceId = (uint)id,
                        RollbackX = rollbackX,
                        RollbackY = rollbackY,
                    };
                    _mapService.BroadcastToMap(mapName, (int)PProtocol.MessageId.GameMonsterMoveCancelNotify, cancelNotify.ToByteArray());
                }
            }

            // ---- 复活检查 ----
            ProcessRespawns();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Monster] tick error");
        }
    }

    // ---- IMonsterRegistry ----

    public void OnDamage(long instanceId, long attackerId, int damage)
    {
        if (!_monsters.TryGetValue(instanceId, out var m)) return;

        // HP 已由 CombatManager 在 MapMonsterState 上扣除；
        // 这里只处理 AI 战斗状态与死亡回调。
        // 回归/强制返回状态下接受伤害但不重新进入战斗，避免回归途中被 pending 伤害拉回追击
        bool isReturning = m.State is MonsterState.Return or MonsterState.ForcedReturn;
        if (!isReturning)
        {
            m.InCombat = true;
            m.TargetId = attackerId;
            m.State = MonsterState.Combat;
        }
        _logger.LogInformation("[Monster] damaged: id={Id} dmg={Dmg} hp={Hp} state={State}", instanceId, damage, m.Hp, m.State);
        if (m.Hp <= 0)
        {
            m.Hp = 0;
            _logger.LogInformation("[Monster] died: id={Id} respawn={RespawnSec}s type={Type}", instanceId, m.RespawnTimeSec, m.RespawnType);

            // 从世界状态移除怪物
            _mapService.World.MonsterLeave(instanceId, m.MapName);
            _monsters.TryRemove(instanceId, out _);

            // 广播死亡通知给地图上的所有玩家
            var notify = new PGame.MonsterDeathNotify
            {
                InstanceId = (uint)instanceId,
                KillerId = (ulong)attackerId,
                MonsterId = (uint)m.MonsterId,
            };
            _mapService.BroadcastToMap(m.MapName, (int)PProtocol.MessageId.GameMonsterDeathNotify, notify.ToByteArray());

            // 加入复活队列
            if (m.RespawnTimeSec > 0)
            {
                _respawnEntries.Add(new RespawnEntry
                {
                    InstanceId = instanceId,
                    MonsterId = m.MonsterId,
                    MapName = m.MapName,
                    SpawnX = m.SpawnX,
                    SpawnY = m.SpawnY,
                    SizeX = m.SizeX,
                    SizeY = m.SizeY,
                    DeathX = m.X,
                    DeathY = m.Y,
                    RespawnTimeSec = m.RespawnTimeSec,
                    DeathTimeMs = Environment.TickCount64,
                    RespawnType = m.RespawnType,
                    RespawnRange = m.RespawnRange,
                    Name = m.Name,
                    Level = m.Level,
                    MaxHp = m.MaxHp,
                    Patk = m.Patk,
                    Matk = m.Matk,
                    Pdef = m.Pdef,
                    Mdef = m.Mdef,
                    MoveSpeedMs = m.MoveSpeedMs,
                    AiType = m.AiType,
                    AiConfig = m.AiConfig,
                });
            }

            OnMonsterDeath?.Invoke(instanceId, attackerId, m.MonsterId, m.MapName, m.X, m.Y);
        }
    }

    public void OnRegen(long instanceId, int regen)
    {
        if (!_monsters.TryGetValue(instanceId, out var m)) return;
        if (m.MapState == null) return;
        m.Hp = Math.Min(m.MaxHp, m.Hp + regen);
    }

    public void OnDisengage(long instanceId)
    {
        if (!_monsters.TryGetValue(instanceId, out var m)) return;
        m.InCombat = false;
        m.TargetId = null;
        m.NarratedTargetId = null;
        m.LastMoveTime = 0;
        m.ChaseTimeoutTimer = 0;

        // 若尚未选择回归巡逻点（CheckChaseTimeout 已选则跳过，避免重复随机导致目标变更）
        if (!m.ReturnPatrolPoint.HasValue)
        {
            SelectReturnPatrolPoint(m);
        }
        m.State = MonsterState.Return;
        m.ReturnCooldownEndMs = Environment.TickCount64 + (long)((m.AiConfig.ChaseTimeout ?? 10.0) * 1000);

        // 应用回归Buff（若已存在则跳过，避免 CheckChaseTimeout + OnDisengage 重复添加）
        if (m.AiConfig.ReturnBuffId is > 0 && CombatManager != null)
        {
            var monsterCtx = CombatManager.GetContext(instanceId);
            if (monsterCtx != null && !monsterCtx.Buffs.HasBuff(m.AiConfig.ReturnBuffId.Value))
            {
                var now = Environment.TickCount64;
                var buffCfg = _tables.GetBuff(m.AiConfig.ReturnBuffId.Value);
                long expireTime = buffCfg != null && buffCfg.Duration > 0
                    ? now + (long)(buffCfg.Duration * 1000)
                    : now + 30000; // 默认30秒
                var buff = new BuffInstance
                {
                    BuffId = m.AiConfig.ReturnBuffId.Value,
                    Stacks = 1,
                    ApplyTime = now,
                    ExpireTime = expireTime,
                    CasterId = instanceId,
                    TargetId = instanceId,
                    LastTickTime = now,
                };
                monsterCtx.Buffs.AddBuff(buff);
            }
        }

        _logger.LogInformation("[Monster] disengaged: id={Id} returnTo=({X},{Y})", instanceId,
            m.ReturnPatrolPoint?.x ?? m.SpawnX, m.ReturnPatrolPoint?.y ?? m.SpawnY);
    }

    private static void SelectReturnPatrolPoint(MonsterRuntimeState m)
    {
        var cfg = m.AiConfig;
        if (cfg.PatrolPoints != null && cfg.PatrolPoints.Count > 0)
        {
            // 随机选一个巡逻点
            var pt = cfg.PatrolPoints[Random.Shared.Next(cfg.PatrolPoints.Count)];
            m.ReturnPatrolPoint = (pt.x, pt.y);
            m.ReturnPatrolIndex = cfg.PatrolPoints.IndexOf(pt);
        }
        else
        {
            m.ReturnPatrolPoint = null;
            m.ReturnPatrolIndex = -1;
        }
    }

    public bool IsOccupied(string mapName, int x, int y)
    {
        foreach (var m in _monsters.Values.ToArray())
            if (m.MapName == mapName && m.X == x && m.Y == y) return true;
        return false;
    }

    public List<MapMonsterState> GetMonstersOnMap(string mapName)
    {
        var result = new List<MapMonsterState>();
        foreach (var m in _monsters.Values.ToArray())
        {
            if (m.MapName == mapName)
            {
                result.Add(new MapMonsterState
                {
                    InstanceId = m.InstanceId,
                    MonsterId = m.MonsterId,
                    Name = m.Name,
                    X = m.X,
                    Y = m.Y,
                    SizeX = m.SizeX > 0 ? m.SizeX : 1,
                    SizeY = m.SizeY > 0 ? m.SizeY : 1,
                    Hp = m.Hp,
                    MaxHp = m.MaxHp,
                    Level = m.Level,
                });
            }
        }
        return result;
    }

    // ---- 内部 ----

    private void BindMonsterToMapState(MonsterRuntimeState m)
    {
        var mapMonster = _mapService.World.GetMonstersOnMap(m.MapName).GetValueOrDefault(m.InstanceId);
        if (mapMonster != null)
        {
            m.MapState = mapMonster;
        }
        else
        {
            _logger.LogWarning("[Monster] failed to bind MapMonsterState: id={Id} map={Map}", m.InstanceId, m.MapName);
        }
    }

    private Dictionary<long, MonsterRuntimeState> InitMonstersForMap(string mapName, int startId)
    {
        var monsters = new Dictionary<long, MonsterRuntimeState>();

        // 从 Luban 配置表读取地图刷新点
        var spawns = _tables.GetSpawnsForMap(mapName);
        if (spawns.Count == 0)
        {
            _logger.LogWarning("[Monster] no Luban spawns for map={Map}, skipping", mapName);
            return monsters;
        }

        int moveSpeedMs = GameConstants.DefaultMonsterMoveSpeedMs;

        for (int i = 0; i < spawns.Count; i++)
        {
            var spawn = spawns[i];
            var monsterTemplate = _tables.Monsters.GetValueOrDefault(spawn.MonsterId);
            if (monsterTemplate == null)
            {
                _logger.LogWarning("[Monster] monster template not found: monsterId={Id}", spawn.MonsterId);
                continue;
            }

            var aiRow = _tables.GetAi(spawn.AiId);
            var aiType = aiRow?.AiType ?? "patrol";
            var (combatBehavior, combatRange) = CombatBehaviorTypeResolver.Resolve(aiRow, monsterTemplate, _tables);
            var (hp, maxHp, patk, matk, pdef, mdef) = _tables.ResolveMonsterAttrs(spawn.MonsterId);

            var id = startId + i;
            var aiCfg = aiRow != null ? new AiConfig
            {
                AiType = aiRow.AiType,
                PatrolRange = aiRow.PatrolRange,
                AggroRange = aiRow.AggroRange,
                MaxChaseDistance = aiRow.MaxChaseDistance,
                MoveIntervalMs = aiRow.MoveIntervalMs,
                ChaseIntervalMs = aiRow.ChaseIntervalMs,
                CombatBehavior = combatBehavior,
                CombatRange = combatRange,
                TerritoryRadius = aiRow.TerritoryRadius,
                ChaseTimeout = aiRow.ChaseTimeout,
                ReturnBuffId = aiRow.ReturnBuffId,
                ReturnSpeedMultiplier = aiRow.ReturnSpeedMultiplier,
            } : new AiConfig { AiType = aiType };

            // 怪物当前 Luban 表未配置 footprint，默认 1x1；后续若支持多格怪物，可从 monsterTemplate 读取
            int sizeX = 1;
            int sizeY = 1;

            // 校验/修正出生点：保证 footprint 内均可行走
            var corrected = _mapData.FindNearestWalkableForFootprint(mapName, spawn.X, spawn.Y, sizeX, sizeY);
            int spawnX = corrected?.x ?? spawn.X;
            int spawnY = corrected?.y ?? spawn.Y;
            if (spawnX != spawn.X || spawnY != spawn.Y)
            {
                _logger.LogWarning("[Monster] init spawn corrected: id={Id} map={Map} from ({OldX},{OldY}) to ({NewX},{NewY}) footprint={SizeX}x{SizeY}",
                    id, mapName, spawn.X, spawn.Y, spawnX, spawnY, sizeX, sizeY);
            }

            monsters[id] = new MonsterRuntimeState
            {
                InstanceId = id,
                MonsterId = spawn.MonsterId,
                Name = monsterTemplate.Name,
                MapName = mapName,
                X = spawnX, Y = spawnY, SpawnX = spawnX, SpawnY = spawnY,
                SizeX = sizeX,
                SizeY = sizeY,
                AiType = aiType, AiConfig = aiCfg,
                Hp = hp, MaxHp = maxHp, Level = monsterTemplate.Level,
                Patk = patk, Matk = matk, Pdef = pdef, Mdef = mdef,
                MoveSpeedMs = moveSpeedMs,
                RespawnTimeSec = spawn.RespawnTime,
                RespawnType = spawn.RespawnType,
                RespawnRange = spawn.RespawnRange,
            };
        }
        return monsters;
    }

    private Dictionary<long, PlayerStateView> GetOnlinePlayers(string mapName)
    {
        var mapPlayers = _mapService.GetPlayersOnMap(mapName);
        var result = new Dictionary<long, PlayerStateView>();
        foreach (var (accountId, p) in mapPlayers)
            result[accountId] = new PlayerStateView
            {
                AccountId = p.AccountId,
                RoleId = p.RoleId,
                RoleName = p.RoleName ?? $"player_{accountId}",
                GridX = p.GridX,
                GridY = p.GridY,
            };
        return result;
    }

    private (int x, int y)? RunOverworldBehavior(MonsterRuntimeState m, string mapName, Dictionary<long, PlayerStateView> players)
    {
        var handler = _handlers.GetValueOrDefault(m.AiType);
        if (handler == null) return null;
        return handler.Run(m, mapName, players, _mapService.World);
    }

    private (int x, int y)? RunCombatBehavior(MonsterRuntimeState m, string mapName, Dictionary<long, PlayerStateView> players)
    {
        var behaviorType = m.AiConfig.CombatBehavior == CombatBehaviorType.Auto
            ? CombatBehaviorType.Melee
            : m.AiConfig.CombatBehavior;
        var handler = _combatBehaviors.GetValueOrDefault(behaviorType) ?? _combatBehaviors[CombatBehaviorType.Melee];
        return handler.Run(m, mapName, players, _mapService.World);
    }

    private void UpdateTerritoryNarration(MonsterRuntimeState monster, Dictionary<long, PlayerStateView> players)
    {
        int patrolRange = monster.AiConfig.PatrolRange ?? 0;
        if (patrolRange <= 0)
        {
            monster.TerritoryPlayers.Clear();
            return;
        }

        var currentlyInside = new HashSet<long>();
        foreach (var player in players.Values)
        {
            if (!IsInsidePatrolTerritory(monster, player, patrolRange))
                continue;

            currentlyInside.Add(player.AccountId);
            if (monster.TerritoryPlayers.Contains(player.AccountId))
                continue;

            BroadcastMonsterNarration(
                monster.MapName,
                monster.InstanceId,
                player.RoleName,
                monster.Name,
                $"{player.RoleName}闯入了{monster.Name}的领地");
        }

        foreach (var playerId in monster.TerritoryPlayers)
        {
            if (currentlyInside.Contains(playerId))
                continue;

            var player = _mapService.GetPlayerOnMap(monster.MapName, playerId);
            string playerName = player?.RoleName ?? $"player_{playerId}";
            BroadcastMonsterNarration(
                monster.MapName,
                monster.InstanceId,
                monster.Name,
                playerName,
                $"{playerName}逃走了,{monster.Name}找不到目标");
        }

        monster.TerritoryPlayers = currentlyInside;
    }

    private void BroadcastMonsterNarration(string mapName, long actorId, string actorName, string targetName, string text)
    {
        if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(text))
            return;

        var notify = new PGame.CombatLogNotify();
        notify.Entries.Add(new PGame.CombatLogEntry
        {
            LogType = PGame.CombatLogType.CombatLogBuff,
            Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ActorName = actorName,
            TargetName = targetName,
            Extra = text,
            Value = 0,
        });
        _mapService.BroadcastToMap(mapName, (int)PProtocol.MessageId.GameCombatLogNotify, notify.ToByteArray());
    }

    private static bool IsInsidePatrolTerritory(MonsterRuntimeState monster, PlayerStateView player, int patrolRange)
    {
        return Math.Abs(player.GridX - monster.SpawnX) <= patrolRange &&
               Math.Abs(player.GridY - monster.SpawnY) <= patrolRange;
    }

    /// <summary>
    /// 检查怪物追击是否超时。目标离开领地后累计计时，超时则断开战斗关系进入回归。
    /// </summary>
    private void CheckChaseTimeout(MonsterRuntimeState m, Dictionary<long, PlayerStateView> players)
    {
        if (!m.TargetId.HasValue) return;

        long targetId = m.TargetId.Value;
        var cfg = m.AiConfig;
        double chaseTimeout = cfg.ChaseTimeout is > 0 ? cfg.ChaseTimeout.Value : 10.0;
        // 领地半径：若未配置（<=0），则回退到 MaxChaseDistance（确保有领地限制）
        int territoryRadius = cfg.TerritoryRadius is > 0 ? cfg.TerritoryRadius.Value : (cfg.MaxChaseDistance is > 0 ? cfg.MaxChaseDistance.Value : 10);

        // 目标是否还在在线玩家列表中
        if (!players.TryGetValue(targetId, out var targetPlayer))
        {
            // 目标已离线/切图，视为离开领地
            m.ChaseTimeoutTimer += 0.5;
        }
        else
        {
            // 判断目标是否在领地内
            bool inTerritory = territoryRadius > 0 &&
                Pathfind.Manhattan(m.SpawnX, m.SpawnY, targetPlayer.GridX, targetPlayer.GridY) <= territoryRadius;

            if (inTerritory)
            {
                m.ChaseTimeoutTimer = 0;
            }
            else
            {
                m.ChaseTimeoutTimer += 0.5; // Tick 间隔 500ms
            }
        }

        if (m.ChaseTimeoutTimer >= chaseTimeout)
        {
            _logger.LogInformation("[Monster] chase timeout: id={Id} target={Target} timer={Timer}s, entering return state", m.InstanceId, targetId, m.ChaseTimeoutTimer);

            // 请求 CombatManager 处理脱战广播（统一走脱战通知流程）
            CombatManager?.RequestDisengage(m.InstanceId, targetId);

            // 清除战斗状态
            m.InCombat = false;
            m.TargetId = null;
            m.ChaseTimeoutTimer = 0;
            m.LastMoveTime = 0;

            // 选择回归巡逻点
            SelectReturnPatrolPoint(m);
            m.State = MonsterState.Return;
            m.ReturnCooldownEndMs = Environment.TickCount64 + (long)((cfg.ChaseTimeout ?? 10.0) * 1000);
        }
    }

    // ---- 复活系统 ----

    private void ProcessRespawns()
    {
        long now = Environment.TickCount64;
        for (int i = _respawnEntries.Count - 1; i >= 0; i--)
        {
            var entry = _respawnEntries[i];
            if (now - entry.DeathTimeMs >= entry.RespawnTimeSec * 1000)
            {
                _respawnEntries.RemoveAt(i);
                DoRespawn(entry);
            }
        }
    }

    private void DoRespawn(RespawnEntry entry)
    {
        var (rx, ry) = ResolveRespawnPosition(entry);

        int sizeX = entry.SizeX > 0 ? entry.SizeX : 1;
        int sizeY = entry.SizeY > 0 ? entry.SizeY : 1;

        // 按 footprint 校验复活点；若不可行走或被建筑覆盖，则 fallback 到出生点再校验
        var corrected = _mapData.FindNearestWalkableForFootprint(entry.MapName, rx, ry, sizeX, sizeY);
        if (corrected == null || corrected.Value.x != rx || corrected.Value.y != ry)
        {
            _logger.LogWarning("[Monster] respawn target ({RX},{RY}) not walkable for footprint {SizeX}x{SizeY}, fallback to nearest",
                rx, ry, sizeX, sizeY);
            corrected = _mapData.FindNearestWalkableForFootprint(entry.MapName, entry.SpawnX, entry.SpawnY, sizeX, sizeY);
            if (corrected != null)
            {
                rx = corrected.Value.x;
                ry = corrected.Value.y;
            }
            else
            {
                rx = entry.SpawnX;
                ry = entry.SpawnY;
            }
        }

        var m = new MonsterRuntimeState
        {
            InstanceId = entry.InstanceId,
            MonsterId = entry.MonsterId,
            Name = entry.Name,
            MapName = entry.MapName,
            X = rx, Y = ry, SpawnX = entry.SpawnX, SpawnY = entry.SpawnY,
            SizeX = sizeX,
            SizeY = sizeY,
            AiType = entry.AiType, AiConfig = entry.AiConfig,
            Hp = entry.MaxHp, MaxHp = entry.MaxHp, Level = entry.Level,
            Patk = entry.Patk, Matk = entry.Matk, Pdef = entry.Pdef, Mdef = entry.Mdef,
            MoveSpeedMs = entry.MoveSpeedMs,
            RespawnTimeSec = entry.RespawnTimeSec,
            RespawnType = entry.RespawnType,
            RespawnRange = entry.RespawnRange,
            State = MonsterState.Idle,
        };

        _monsters[entry.InstanceId] = m;
        _mapService.MonsterEnter(entry.InstanceId, entry.MonsterId, entry.MapName, entry.Name, rx, ry, m.Hp, m.MaxHp, m.Level,
            m.Patk, m.Matk, m.Pdef, m.Mdef, m.SizeX, m.SizeY);
        BindMonsterToMapState(m);

        _logger.LogInformation("[Monster] respawned: id={Id} map={Map} pos=({X},{Y}) type={Type}", entry.InstanceId, entry.MapName, rx, ry, entry.RespawnType);

        // 广播复活通知
        var notify = new PGame.MonsterRespawnNotify
        {
            InstanceId = (uint)entry.InstanceId,
            MonsterId = (uint)entry.MonsterId,
            X = rx,
            Y = ry,
            Name = entry.Name,
            Level = (uint)entry.Level,
            SizeX = m.SizeX,
            SizeY = m.SizeY,
        };
        _mapService.BroadcastToMap(entry.MapName, (int)PProtocol.MessageId.GameMonsterRespawnNotify, notify.ToByteArray());
    }

    private (int x, int y) ResolveRespawnPosition(RespawnEntry entry)
    {
        switch (entry.RespawnType)
        {
            case ERespawnType.DeathPoint: // 原地复活
                return (entry.DeathX, entry.DeathY);

            case ERespawnType.RandomNearSpawn: // 出生点附近随机
                int range = entry.RespawnRange;
                int sizeX = entry.SizeX > 0 ? entry.SizeX : 1;
                int sizeY = entry.SizeY > 0 ? entry.SizeY : 1;
                if (range <= 0) return (entry.SpawnX, entry.SpawnY);

                var candidates = new List<(int, int)>();
                for (int dx = -range; dx <= range; dx++)
                {
                    for (int dy = -range; dy <= range; dy++)
                    {
                        int tx = entry.SpawnX + dx;
                        int ty = entry.SpawnY + dy;
                        if (_mapData.FindNearestWalkableForFootprint(entry.MapName, tx, ty, sizeX, sizeY) == (tx, ty))
                            candidates.Add((tx, ty));
                    }
                }

                if (candidates.Count > 0)
                {
                    var rnd = new Random();
                    return candidates[rnd.Next(candidates.Count)];
                }
                return (entry.SpawnX, entry.SpawnY);

            default: // SpawnPoint = 出生点
                return (entry.SpawnX, entry.SpawnY);
        }
    }
}
