using GameServer.Common.Config;
using GameServer.Services.Core;
using GameServer.Services.Map;
using GameServer.Services.Monster.AI;
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

    private Dictionary<long, MonsterRuntimeState> _monsters = new();

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
        _monsters = new Dictionary<long, MonsterRuntimeState>();

        _tables.Load();

        int nextId = 1000001;
        foreach (var (mapName, _) in _mapData.GetAllMaps())
        {
            var mapMonsters = InitMonstersForMap(mapName, nextId);
            foreach (var (instanceId, m) in mapMonsters)
            {
                _monsters[instanceId] = m;
                _mapService.MonsterEnter(instanceId, m.MonsterId, mapName, m.Name, m.X, m.Y, m.Hp, m.MaxHp, m.Level,
                    m.Patk, m.Matk, m.Pdef, m.Mdef, m.Agility);
                _logger.LogInformation("[Monster] init: id={InstanceId} map={Map} pos=({X},{Y}) ai={Ai}", instanceId, mapName, m.X, m.Y, m.AiType);
            }
            nextId += mapMonsters.Count;
        }

        _logger.LogInformation("[Monster] total={Count} across {Maps} maps", _monsters.Count, _mapData.GetAllMaps().Count);
    }

    public void Tick()
    {
        try
        {
            // 按地图分组处理
            var byMap = _monsters.GroupBy(kv => kv.Value.MapName).ToList();
            foreach (var group in byMap)
            {
                var mapName = group.Key;
                var players = GetOnlinePlayers(mapName);
                var movedMonsters = new List<(long id, int fx, int fy, int tx, int ty, string state, int durationMs)>();

                foreach (var (instanceId, m) in group)
                {
                    // ---- 0. 战斗中的怪物不做 AI 和碰撞检测 ----
                    if (m.InCombat) continue;

                    // ---- 1. 处理正在移动的怪物 ----
                    if (m.IsMoving)
                    {
                        long elapsed = Environment.TickCount64 - m.MoveStartTime;
                        if (elapsed >= m.MoveSpeedMs)
                        {
                            // 移动完成：确认坐标
                            _mapService.World.ConfirmMove(instanceId);
                            _mapService.World.CompleteMove(instanceId);
                            m.X = m.MoveTargetX;
                            m.Y = m.MoveTargetY;
                            m.IsMoving = false;

                            // 到达新格后，统一碰撞检测（与玩家一致）
                            _mapService.CheckEntityCollision(instanceId, mapName, m.X, m.Y);
                        }
                        continue; // 移动中不执行 AI
                    }

                    // ---- 2. AI 决策 ----
                    var handler = _handlers.GetValueOrDefault(m.AiType);
                    if (handler == null) continue;

                    var next = handler.Run(m, mapName, players);
                    if (next == null) continue;

                    var (nx, ny) = next.Value;
                    if (nx == m.X && ny == m.Y) continue;

                    // ---- 3. 预占目标格 ----
                    int durationMs = m.MoveSpeedMs > 0 ? m.MoveSpeedMs : 800;
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
                        // 目标格被占据，检查碰撞（怪物试图走入玩家位置 → 触发战斗）
                        _mapService.CheckEntityCollision(instanceId, mapName, m.X, m.Y);
                    }
                }

                // 按地图广播
                foreach (var (id, fx, fy, tx, ty, state, durationMs) in movedMonsters)
                {
                    var notify = new PGame.MonsterMoveNotify
                    {
                        InstanceId = (uint)id,
                        FromX = fx,
                        FromY = fy,
                        ToX = tx,
                        ToY = ty,
                        State = state,
                        DurationMs = durationMs,
                    };
                    _mapService.BroadcastToMap(mapName, (int)PProtocol.MessageId.GameMonsterMoveNotify, notify.ToByteArray());
                }
            }
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
        m.Hp -= damage;
        m.InCombat = true;
        _logger.LogInformation("[Monster] damaged: id={Id} dmg={Dmg} hp={Hp}", instanceId, damage, m.Hp);
        if (m.Hp <= 0)
        {
            m.Hp = 0;
            _logger.LogInformation("[Monster] died: id={Id}", instanceId);
            // TODO: 死亡处理（掉落、经验分配）
        }
    }

    public void OnRegen(long instanceId, int regen)
    {
        if (!_monsters.TryGetValue(instanceId, out var m)) return;
        m.Hp = Math.Min(m.MaxHp, m.Hp + regen);
    }

    public void OnDisengage(long instanceId)
    {
        if (!_monsters.TryGetValue(instanceId, out var m)) return;
        m.InCombat = false;
        _logger.LogInformation("[Monster] disengaged: id={Id}", instanceId);
    }

    public bool IsOccupied(string mapName, int x, int y)
    {
        foreach (var m in _monsters.Values)
            if (m.MapName == mapName && m.X == x && m.Y == y) return true;
        return false;
    }

    public List<MapMonsterState> GetMonstersOnMap(string mapName)
    {
        var result = new List<MapMonsterState>();
        foreach (var m in _monsters.Values)
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
                    Hp = m.Hp,
                    MaxHp = m.MaxHp,
                    Level = m.Level,
                });
            }
        }
        return result;
    }

    // ---- 内部 ----

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

        int moveSpeedMs = 800; // TODO: move to Luban GlobalConfig

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
            var (hp, maxHp, patk, matk, pdef, mdef, agility) = _tables.ResolveMonsterAttrs(spawn.MonsterId);

            var id = startId + i;
            var aiCfg = aiRow != null ? new AiConfig
            {
                AiType = aiRow.AiType,
                PatrolRange = aiRow.PatrolRange,
                AggroRange = aiRow.AggroRange,
                MaxChaseDistance = aiRow.MaxChaseDistance,
                MoveIntervalMs = aiRow.MoveIntervalMs,
                ChaseIntervalMs = aiRow.ChaseIntervalMs,
            } : new AiConfig { AiType = aiType };

            monsters[id] = new MonsterRuntimeState
            {
                InstanceId = id,
                MonsterId = spawn.MonsterId,
                Name = monsterTemplate.Name,
                MapName = mapName,
                X = spawn.X, Y = spawn.Y, SpawnX = spawn.X, SpawnY = spawn.Y,
                AiType = aiType, AiConfig = aiCfg,
                Hp = hp, MaxHp = maxHp, Level = monsterTemplate.Level,
                Patk = patk, Matk = matk, Pdef = pdef, Mdef = mdef, Agility = agility,
                MoveSpeedMs = moveSpeedMs,
            };
        }
        return monsters;
    }

    private Dictionary<long, PlayerStateView> GetOnlinePlayers(string mapName)
    {
        var mapPlayers = _mapService.GetPlayersOnMap(mapName);
        var result = new Dictionary<long, PlayerStateView>();
        foreach (var (accountId, p) in mapPlayers)
            result[accountId] = new PlayerStateView { AccountId = p.AccountId, RoleId = p.RoleId, GridX = p.GridX, GridY = p.GridY };
        return result;
    }
}
