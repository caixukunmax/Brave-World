using GameServer.Common.Config;
using GameServer.Services.Core;
using GameServer.Services.Map;
using GameServer.Services.Map.Combat;
using GameServer.Services.Map.Combat.Actions;
using GameServer.Services.Monster;
using GameServer.Services.Player;
using GameServer.Services.Player.Handlers;
using GameServer.Tables;
using Microsoft.Extensions.Logging;

namespace GameServer.GameLogic;

/// <summary>
/// 游戏逻辑工厂 — 宿主通过 ALC 加载后调用此工厂创建所有逻辑对象
/// </summary>
public class GameLogicFactory : IGameLogicFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private LubanTableLoader? _tables;

    public GameLogicFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public LubanTableLoader Tables
    {
        get
        {
            if (_tables == null)
            {
                _tables = new LubanTableLoader(_loggerFactory.CreateLogger<LubanTableLoader>());
                _tables.Load();
            }
            return _tables;
        }
    }

    public ICombatService CreateCombatService(ILogger logger, INetworkSender network)
    {
        var actionRegistry = new ActionRegistry();
        actionRegistry.Register(new DealDamageAction(
            _loggerFactory.CreateLogger<DealDamageAction>()));
        actionRegistry.Register(new InterruptCastAction(
            _loggerFactory.CreateLogger<InterruptCastAction>()));

        var pipeline = new SkillPipeline(
            _loggerFactory.CreateLogger<SkillPipeline>(), actionRegistry);

        var combatManager = new CombatManager(
            _loggerFactory.CreateLogger<CombatManager>(), pipeline, actionRegistry, network);

        return new CombatServiceAdapter(combatManager);
    }

    public IMonsterAiService CreateMonsterAiService(
        MapDataProvider mapData, MapService mapService, INetworkSender network)
    {
        var monsterManager = new MonsterManager(
            _loggerFactory.CreateLogger<MonsterManager>(), mapData, mapService, network, Tables);
        return new MonsterAiServiceAdapter(monsterManager);
    }

    public void RegisterMessageHandlers(
        MessageHandlerRegistry registry, PlayerSessionManager session, INetworkSender network, MapDataProvider mapData, IMonsterAiService monsterAi)
    {
        registry.Add((int)Protocol.MessageId.GameCreateRoleReq, new CreateRoleHandler(session, mapData));
        registry.Add((int)Protocol.MessageId.GameEnterGameReq, new EnterGameHandler(session, network, monsterAi, Tables));
        registry.Add((int)Protocol.MessageId.GameMoveReq, new MoveStartHandler(session, network, _loggerFactory.CreateLogger<MoveStartHandler>()));
        registry.Add((int)Protocol.MessageId.GameMoveConfirmReq, new MoveConfirmHandler(session, network, _loggerFactory.CreateLogger<MoveConfirmHandler>()));
        registry.Add((int)Protocol.MessageId.GameMoveCompleteReq, new MoveCompleteHandler(session));
        registry.Add((int)Protocol.MessageId.GameUseItemReq, new UseItemHandler(session));
        registry.Add((int)Protocol.MessageId.GameDropItemReq, new DropItemHandler(session));
        registry.Add((int)Protocol.MessageId.GameGmReq, new GmCommandHandler(session, network));
        registry.Add((int)Protocol.MessageId.GameOpenChestReq, new OpenChestHandler(session));
        registry.Add((int)Protocol.MessageId.GameUpdateUiPanelPosReq, new UpdateUIPanelPosHandler(session));
        registry.Add((int)Protocol.MessageId.GameChangeMapReq, new ChangeMapHandler(session, network, mapData, monsterAi, Tables));
    }
}

/// <summary>
/// CombatManager 适配器 — 暴露 ICombatService 接口
/// </summary>
internal class CombatServiceAdapter : ICombatService
{
    private readonly CombatManager _inner;
    public CombatServiceAdapter(CombatManager inner) => _inner = inner;
    public CombatManager Inner => _inner;

    public void OnCollision(long entityA, long entityB, Dictionary<string, MapState> maps)
        => _inner.OnCollision(entityA, entityB, maps);

    public void Tick(double dt, Dictionary<string, MapState> maps, IMonsterRegistry? monsterRegistry)
        => _inner.Tick(dt, maps, monsterRegistry);

    public bool IsCasting(long entityId) => _inner.IsCasting(entityId);
}

/// <summary>
/// MonsterManager 适配器 — 暴露 IMonsterAiService + IMonsterRegistry 接口
/// </summary>
internal class MonsterAiServiceAdapter : IMonsterAiService, IMonsterRegistry
{
    private readonly MonsterManager _inner;
    public MonsterAiServiceAdapter(MonsterManager inner) => _inner = inner;
    public MonsterManager Inner => _inner;

    public void Init() => _inner.Init();
    public void Tick() => _inner.Tick();
    public void OnDamage(long instanceId, long attackerId, int damage) => _inner.OnDamage(instanceId, attackerId, damage);
    public void OnRegen(long instanceId, int regen) => _inner.OnRegen(instanceId, regen);
    public bool IsOccupied(string mapName, int x, int y) => _inner.IsOccupied(mapName, x, y);
    public void OnDisengage(long instanceId) => _inner.OnDisengage(instanceId);
    public List<MapMonsterState> GetMonstersOnMap(string mapName) => _inner.GetMonstersOnMap(mapName);
}
