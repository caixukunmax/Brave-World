using System.Reflection;
using System.Runtime.Loader;
using GameServer.Common.Net;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Core;

/// <summary>
/// ALC 热加载管理器 — 加载/卸载/重载 GameServer.GameLogic.dll
/// </summary>
public class HotReloader
{
    private readonly ILogger<HotReloader> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _dllPath;
    private CollectibleAssemblyLoadContext? _alc;
    private IGameLogicFactory? _factory;

    public IGameLogicFactory? Factory => _factory;
    public ICombatService? CombatService { get; private set; }
    public IMonsterAiService? MonsterService { get; private set; }

    public HotReloader(ILoggerFactory loggerFactory, string? dllPath = null)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<HotReloader>();
        _dllPath = dllPath ?? Path.Combine(AppContext.BaseDirectory, "GameServer.GameLogic.dll");
    }

    /// <summary>
    /// 首次加载 GameLogic DLL
    /// </summary>
    public IGameLogicFactory Load()
    {
        _logger.LogInformation("[HotReload] Loading {Dll}", _dllPath);
        _alc = new CollectibleAssemblyLoadContext();
        var assembly = _alc.LoadFromAssemblyPath(_dllPath);
        var factoryType = assembly.GetType("GameServer.GameLogic.GameLogicFactory")
            ?? throw new InvalidOperationException("GameLogicFactory type not found");

        _factory = (IGameLogicFactory)Activator.CreateInstance(factoryType, _loggerFactory)!;
        _logger.LogInformation("[HotReload] Factory created: {Type}", factoryType.FullName);
        return _factory;
    }

    /// <summary>
    /// 使用工厂创建逻辑对象
    /// </summary>
    public void CreateServices(
        IGameLogicFactory factory,
        ILogger logger,
        INetworkSender network,
        Common.Config.MapDataProvider mapData,
        Map.MapService mapService,
        MessageHandlerRegistry handlerRegistry,
        Player.PlayerSessionManager playerSession)
    {
        CombatService = factory.CreateCombatService(logger, network);
        MonsterService = factory.CreateMonsterAiService(mapData, mapService, network);
        playerSession.CombatService = CombatService;
        factory.RegisterMessageHandlers(handlerRegistry, playerSession, network, mapData, MonsterService);
        _logger.LogInformation("[HotReload] Services created and handlers registered");
    }

    /// <summary>
    /// 卸载当前 GameLogic DLL，准备加载新版本
    /// </summary>
    public void Unload()
    {
        CombatService = null;
        MonsterService = null;
        _factory = null;

        if (_alc != null)
        {
            _logger.LogInformation("[HotReload] Unloading old ALC...");
            _alc.Unload();
            _alc = null;

            // 强制 GC 确保旧程序集被回收
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            _logger.LogInformation("[HotReload] Old ALC unloaded");
        }
    }

    /// <summary>
    /// 热重载：卸载旧版本 → 加载新版本 → 重建服务
    /// </summary>
    public void Reload(
        ILogger logger,
        INetworkSender network,
        Common.Config.MapDataProvider mapData,
        Map.MapService mapService,
        MessageHandlerRegistry handlerRegistry,
        Player.PlayerSessionManager playerSession,
        MessageRouter router)
    {
        _logger.LogInformation("[HotReload] === Starting hot reload ===");

        // 1. 卸载旧版本
        Unload();

        // 2. 清理旧的消息路由
        handlerRegistry.Clear();

        // 3. 加载新版本
        var factory = Load();

        // 4. 重建服务
        CreateServices(factory, logger, network, mapData, mapService, handlerRegistry, playerSession);

        // 5. 重新注册路由
        handlerRegistry.RegisterAll(router);

        _logger.LogInformation("[HotReload] === Hot reload complete ===");
    }
}

/// <summary>
/// 可卸载的 ALC — 允许运行时卸载程序集
/// </summary>
internal class CollectibleAssemblyLoadContext : AssemblyLoadContext
{
    public CollectibleAssemblyLoadContext() : base(isCollectible: true) { }

    protected override Assembly? Load(AssemblyName name)
    {
        // 优先从默认上下文加载（共享 Common/Services 等基础程序集）
        return null;
    }
}
