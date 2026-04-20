using GameServer.Common.Config;
using GameServer.Common.Events;
using GameServer.Common.Net;
using GameServer.Common.Security;
using GameServer.Database;
using GameServer.Database.Repositories;
using GameServer.Services.Gateway;
using GameServer.Services.Login;
using GameServer.Services.Map;
using GameServer.Services.Core;
using GameServer.Services.Player;
using GameServer.Services.World;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Serilog;

namespace GameServer;

class Program
{
    static async Task Main(string[] args)
    {
        GameConstants.LoadFromConfig();
        Directory.CreateDirectory("logs");
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/server-.log", rollingInterval: RollingInterval.Day)
            .MinimumLevel.Debug()
            .CreateLogger();

        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                var config = context.Configuration;

                // Infrastructure
                services.AddSingleton<EventBus>();
                services.AddSingleton<MessageRouter>();
                services.AddSingleton<TokenGenerator>(
                    new TokenGenerator(config["Game:TokenSecret"] ?? "tslua2_game_secret_2024"));
                services.AddSingleton<MapDataProvider>();

                // Database
                var mongoHost = config["MongoDB:Host"] ?? "127.0.0.1";
                var mongoPort = int.Parse(config["MongoDB:Port"] ?? "27017");
                var mongoDb = config["MongoDB:Database"] ?? "tslua2";
                services.AddSingleton<MongoDbContext>(new MongoDbContext(mongoHost, mongoPort, mongoDb));
                services.AddSingleton<CounterRepository>();
                services.AddSingleton<AccountRepository>();
                services.AddSingleton<RoleRepository>();
                services.AddSingleton<ServerRepository>();
                services.AddSingleton<InventoryRepository>();
                services.AddSingleton<ChestRepository>();

                // Gateway
                services.AddSingleton<GatewayService>(sp =>
                {
                    var port = int.Parse(config["Gateway:Port"] ?? "8889");
                    var hbTimeout = int.Parse(config["Gateway:HeartbeatTimeoutSeconds"] ?? "3600");
                    return new GatewayService(
                        sp.GetRequiredService<ILogger<GatewayService>>(),
                        sp.GetRequiredService<MessageRouter>(),
                        port,
                        hbTimeout);
                });
                services.AddSingleton<INetworkSender>(sp => sp.GetRequiredService<GatewayService>());

                // Core infrastructure
                services.AddSingleton<AuthMiddleware>();
                services.AddSingleton<MessageHandlerRegistry>();
                services.AddSingleton<WorldState>();
                services.AddSingleton<CollisionDetector>();

                // Services (host framework)
                services.AddSingleton<MapService>();
                services.AddSingleton<LoginService>();
                services.AddSingleton<PlayerSessionManager>();

                // Hot reloader
                services.AddSingleton<HotReloader>();

                // Hosted service
                services.AddSingleton<GameServerHostedService>();
                services.AddHostedService(sp => sp.GetRequiredService<GameServerHostedService>());
            })
            .Build();

        await host.RunAsync();
    }
}

/// <summary>
/// 服务器主生命周期服务
/// </summary>
public class GameServerHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _config;
    private readonly ILogger<GameServerHostedService> _logger;
    private CancellationTokenSource _cts = new();

    public GameServerHostedService(IServiceProvider sp, IConfiguration config, ILogger<GameServerHostedService> logger)
    {
        _sp = sp;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("======== Game Server Starting ========");

        // 1. 初始化数据库
        var accountRepo = _sp.GetRequiredService<AccountRepository>();
        var roleRepo = _sp.GetRequiredService<RoleRepository>();
        var chestRepo = _sp.GetRequiredService<ChestRepository>();
        var serverRepo = _sp.GetRequiredService<ServerRepository>();

        await accountRepo.EnsureIndex();
        await roleRepo.EnsureIndex();
        await chestRepo.CleanupOldGmChests();
        await SeedServers(serverRepo);
        _logger.LogInformation("MongoDB connected and indexed");

        // 2. 加载地图数据
        var mapData = _sp.GetRequiredService<MapDataProvider>();
        var config = _sp.GetRequiredService<IConfiguration>();
        var dataDir = _config["GameData:Dir"] ?? "data";
        var mapCount = mapData.LoadAllMaps(dataDir);
        _logger.LogInformation("Loaded {Count} maps from {Dir}", mapCount, dataDir);

        // 3. 加载 GameLogic (通过 ALC 热加载)
        var hotReloader = _sp.GetRequiredService<HotReloader>();
        var network = _sp.GetRequiredService<GatewayService>();
        var mapService = _sp.GetRequiredService<MapService>();
        var handlerRegistry = _sp.GetRequiredService<MessageHandlerRegistry>();
        var playerSession = _sp.GetRequiredService<PlayerSessionManager>();

        var factory = hotReloader.Load();
        hotReloader.CreateServices(factory, _logger, network, mapData, mapService, handlerRegistry, playerSession);

        // 4. 注册消息路由
        var router = _sp.GetRequiredService<MessageRouter>();
        var gateway = _sp.GetRequiredService<GatewayService>();
        var loginService = _sp.GetRequiredService<LoginService>();
        loginService.RegisterRoutes(router, gateway);
        handlerRegistry.RegisterAll(router);
        _logger.LogInformation("Message routes registered");

        // 5. 订阅碰撞事件到战斗系统
        var eventBus = _sp.GetRequiredService<EventBus>();
        eventBus.On("CollisionDetected", (data) =>
        {
            var (entityA, entityB, mapName) = ((long entityA, long entityB, string mapName))data!;
            var maps = mapService.GetAllMapsLegacy();
            hotReloader.CombatService?.OnCollision(entityA, entityB, maps);
        });

        // 初始化怪物
        hotReloader.MonsterService?.Init();
        _logger.LogInformation("Combat & Monster initialized");

        // 6. 启动游戏 tick 定时器
        _ = MonsterTickLoop(hotReloader, _cts.Token);
        _ = CombatTickLoop(hotReloader, mapService, _cts.Token);
        _ = HotReloadCommandLoop(hotReloader, _logger, network, mapData, mapService, handlerRegistry, playerSession, router, _cts.Token);
        _logger.LogInformation("Game tick loops started");

        // 7. 启动 Gateway
        _ = gateway.StartAsync(_cts.Token);
        _logger.LogInformation("======== Game Server Ready ========");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        _logger.LogInformation("======== Game Server Stopped ========");
        return Task.CompletedTask;
    }

    private async Task SeedServers(ServerRepository serverRepo)
    {
        var servers = new List<Database.Models.ServerInfo>
        {
            new()
            {
                ServerId = 1,
                ServerName = "测试服",
                Host = "127.0.0.1",
                Port = 8001,
                Status = 1,
                IsNew = true,
                IsRecommend = true,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            },
        };
        await serverRepo.Seed(servers);
    }

    private static async Task MonsterTickLoop(HotReloader hotReloader, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(GameConstants.MonsterAiTickMs));
        while (await timer.WaitForNextTickAsync(ct))
            hotReloader.MonsterService?.Tick();
    }

    private static async Task CombatTickLoop(
        HotReloader hotReloader,
        MapService mapService,
        CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(GameConstants.CombatTickMs));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var maps = mapService.GetAllMapsLegacy();
            hotReloader.CombatService?.Tick(0.1, maps, hotReloader.MonsterService as IMonsterRegistry);
            mapService.SyncCombatHp(maps);
        }
    }

    /// <summary>
    /// 控制台热更命令监听 — 输入 "reload" 触发热更
    /// </summary>
    private static async Task HotReloadCommandLoop(
        HotReloader hotReloader,
        Microsoft.Extensions.Logging.ILogger logger,
        GatewayService network,
        MapDataProvider mapData,
        MapService mapService,
        MessageHandlerRegistry handlerRegistry,
        PlayerSessionManager playerSession,
        MessageRouter router,
        CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(ct);
                if (line?.Trim() == "reload")
                {
                    try
                    {
                        hotReloader.Reload(logger, network, mapData, mapService, handlerRegistry, playerSession, router);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[HotReload] Failed");
                    }
                }
            }
        }, ct);
    }
}
