using System.Reflection;
using Google.Protobuf;
using GameServer.Common.Config;
using GameServer.Common.Events;
using GameServer.Services.Core;
using GameServer.Services.Map;
using GameServer.Services.Map.Combat;
using GameServer.Services.Map.Combat.Actions;
using GameServer.Services.Monster;
using GameServer.Services.Player;
using GameServer.GameLogic.Npc;
using GameServer.Services.World;
using GameServer.Tables;
using Microsoft.Extensions.Logging;

namespace GameServer.GameLogic;

/// <summary>
/// 游戏逻辑工厂 — 宿主通过 ALC 加载后调用此工厂创建所有逻辑对象
/// </summary>
public class GameLogicFactory : IGameLogicFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly LubanTableLoader _tables;

    public GameLogicFactory(ILoggerFactory loggerFactory, LubanTableLoader tables)
    {
        _loggerFactory = loggerFactory;
        _tables = tables;
    }

    public LubanTableLoader Tables => _tables;

    public ICombatService CreateCombatService(ILogger logger, INetworkSender network)
    {
        var actionRegistry = new ActionRegistry();
        actionRegistry.Register(new DealDamageAction(
            _loggerFactory.CreateLogger<DealDamageAction>()));
        actionRegistry.Register(new InterruptCastAction(
            _loggerFactory.CreateLogger<InterruptCastAction>()));
        actionRegistry.Register(new HealAction(
            _loggerFactory.CreateLogger<HealAction>()));
        actionRegistry.Register(new ApplyBuffAction(Tables));
        actionRegistry.Register(new PurifyAction(
            _loggerFactory.CreateLogger<PurifyAction>()));

        var pipeline = new SkillPipeline(
            _loggerFactory.CreateLogger<SkillPipeline>(), actionRegistry, Tables);

        var combatManager = new CombatManager(
            _loggerFactory.CreateLogger<CombatManager>(), pipeline, actionRegistry, network, Tables);

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
        MessageHandlerRegistry registry,
        PlayerSessionManager session,
        INetworkSender network,
        MapDataProvider mapData,
        IMonsterAiService monsterAi,
        WorldState worldState,
        EventBus eventBus)
    {
        // 构建依赖解析表
        var dependencies = new Dictionary<Type, Func<object>>
        {
            [typeof(PlayerSessionManager)] = () => session,
            [typeof(INetworkSender)] = () => network,
            [typeof(MapDataProvider)] = () => mapData,
            [typeof(IMonsterAiService)] = () => monsterAi,
            [typeof(WorldState)] = () => worldState,
            [typeof(IWorldState)] = () => worldState,
            [typeof(EventBus)] = () => eventBus,
            [typeof(LubanTableLoader)] = () => Tables,
        };

        // 扫描当前程序集中所有 IMessageHandler 实现
        var handlerTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IMessageHandler).IsAssignableFrom(t));

        foreach (var handlerType in handlerTypes)
        {
            var attr = handlerType.GetCustomAttribute<HandlesMessageAttribute>();
            if (attr == null)
            {
                // 无 HandlesMessageAttribute 的 IMessageHandler 实现类，跳过
                continue;
            }

            var handler = CreateHandler(handlerType, dependencies);
            registry.Add(attr.MessageId, handler);
        }
    }

    /// <summary>
    /// 通过反射创建 handler 实例，自动解析构造函数参数
    /// </summary>
    private IMessageHandler CreateHandler(Type handlerType, Dictionary<Type, Func<object>> dependencies)
    {
        var ctors = handlerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (ctors.Length == 0)
            throw new InvalidOperationException(
                $"No public constructor found for handler '{handlerType.Name}'");

        // 优先使用参数最多的构造函数（通常只有一个公开构造函数）
        var ctor = ctors.Length == 1 ? ctors[0] : ctors.OrderByDescending(c => c.GetParameters().Length).First();

        var args = new object?[ctor.GetParameters().Length];
        for (int i = 0; i < args.Length; i++)
        {
            var paramType = ctor.GetParameters()[i].ParameterType;

            // ILogger<T> 特殊处理
            if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(ILogger<>))
            {
                // LoggerFactoryExtensions.CreateLogger<T>(this ILoggerFactory) 是扩展方法
                // 它返回的 Logger<T> 实现了 ILogger<T>
                var loggerType = paramType.GetGenericArguments()[0];
                var createLoggerOpen = typeof(LoggerFactoryExtensions)
                    .GetMethods()
                    .First(m => m.Name == "CreateLogger" && m.IsGenericMethod && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == typeof(ILoggerFactory));
                var genericMethod = createLoggerOpen.MakeGenericMethod(loggerType);
                args[i] = genericMethod.Invoke(null, new object[] { _loggerFactory });
                continue;
            }

            if (dependencies.TryGetValue(paramType, out var factory))
            {
                args[i] = factory();
                continue;
            }

            throw new InvalidOperationException(
                $"Cannot resolve parameter '{ctor.GetParameters()[i].Name}' of type '{paramType.Name}' " +
                $"for handler '{handlerType.Name}'. Supported types: " +
                $"{string.Join(", ", dependencies.Keys.Select(k => k.Name))}, ILogger<T>");
        }

        return (IMessageHandler)ctor.Invoke(args);
    }

    public void BindDeathHandler(ICombatService combatService, MapService mapService, MapDataProvider mapData, PlayerSessionManager session, INetworkSender network)
    {
        if (combatService is not CombatServiceAdapter adapter) return;
        var responder = new DeathResponder(
            _loggerFactory.CreateLogger<DeathResponder>(),
            mapService, mapData, session, Tables, network);
        adapter.Inner.DeathCallback = responder.OnPlayerDeath;
    }

    public void BindMonsterRegistry(ICombatService combatService, IMonsterAiService monsterAi)
    {
        if (combatService is not CombatServiceAdapter combatAdapter) return;
        if (monsterAi is IMonsterRegistry registry)
            combatAdapter.Inner.MonsterRegistry = registry;
    }

    public void BindLevelUpService(
        ICombatService combatService,
        IMonsterAiService monsterAi,
        MapService mapService,
        PlayerSessionManager session,
        INetworkSender network)
    {
        if (monsterAi is not MonsterAiServiceAdapter monsterAdapter) return;

        var levelUpService = new LevelUpService(
            _loggerFactory.CreateLogger<LevelUpService>(),
            session, Tables, network, mapService);

        monsterAdapter.Inner.OnMonsterDeath = levelUpService.OnMonsterDeath;
    }

    public INpcManager InitNpcs(WorldState worldState)
    {
        var npcManager = new NpcManager(worldState, _loggerFactory.CreateLogger<NpcManager>());
        npcManager.Init();
        return npcManager;
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

    public byte[]? HandleCastRequest(long playerId, int skillId, long? targetId, Dictionary<string, MapState> maps)
        => _inner.HandleCastRequest(playerId, skillId, targetId, maps).ToByteArray();
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
