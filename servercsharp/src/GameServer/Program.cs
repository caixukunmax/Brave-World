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
using GameServer.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Google.Protobuf;
using Serilog;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer;

/// <summary>自动保存用的玩家字段快照 — 在 GameLoop 线程读取，避免与保存线程竞争</summary>
file record RoleSaveSnapshot(
    int Hp, int MaxHp, int Mp, int MaxMp,
    int Patk, int Matk, int Pdef, int Mdef,
    int MpRegen, int MoveSpeedMs,
    int GridX, int GridY,
    string CurrentMap,
    int Level, int Exp,
    string Job,
    int PreferredSkillId,
    List<int> LearnedSkills,
    List<int> EquippedSkills);

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

                // Token — 优先从环境变量读取，其次配置，最后回退（仅开发）
                var tokenSecret = Environment.GetEnvironmentVariable("TSLUA2_TOKEN_SECRET")
                    ?? config["Game:TokenSecret"]
                    ?? "tslua2_game_secret_2024";
                services.AddSingleton<TokenGenerator>(
                    new TokenGenerator(tokenSecret));

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
                        sp.GetRequiredService<IGameLoopScheduler>(),
                        port,
                        hbTimeout);
                });
                services.AddSingleton<INetworkSender>(sp => sp.GetRequiredService<GatewayService>());

                // Core infrastructure
                services.AddSingleton<AuthMiddleware>();
                services.AddSingleton<MessageHandlerRegistry>();
                services.AddSingleton<MessageRouter>();
                services.AddSingleton<EventBus>();
                services.AddSingleton<IGameLoopScheduler, GameLoopScheduler>();
                services.AddSingleton<BuildingConfigProvider>();
                services.AddSingleton<MapDataProvider>(sp =>
                {
                    var tables = sp.GetService<LubanTableLoader>();
                    var buildings = sp.GetService<BuildingConfigProvider>();
                    return new MapDataProvider(tables, buildings);
                });
                services.AddSingleton<WorldState>();
                services.AddSingleton<CollisionDetector>();

                // Luban 配置表
                services.AddSingleton<LubanTableLoader>(sp =>
                {
                    var loader = new LubanTableLoader(sp.GetRequiredService<ILogger<LubanTableLoader>>());
                    loader.Load();
                    return loader;
                });

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

        // 2. 加载建筑配置与地图数据
        var buildingConfig = _sp.GetRequiredService<BuildingConfigProvider>();
        var config = _sp.GetRequiredService<IConfiguration>();
        var dataDir = RuntimeDataPathResolver.ResolveMapDataDir(_config["GameData:Dir"]);
        var buildingsPath = Path.Combine(dataDir, "buildings.json");
        buildingConfig.Load(buildingsPath);

        var mapData = _sp.GetRequiredService<MapDataProvider>();
        var mapCount = mapData.LoadAllMaps(dataDir);
        _logger.LogInformation("Loaded {Count} maps from {Dir}", mapCount, dataDir);

        // 3. 加载 GameLogic (通过 ALC 热加载)
        var hotReloader = _sp.GetRequiredService<HotReloader>();
        var network = _sp.GetRequiredService<GatewayService>();
        var mapService = _sp.GetRequiredService<MapService>();
        var handlerRegistry = _sp.GetRequiredService<MessageHandlerRegistry>();
        var playerSession = _sp.GetRequiredService<PlayerSessionManager>();

        var factory = hotReloader.Load();
        var worldState = _sp.GetRequiredService<WorldState>();
        var eventBus = _sp.GetRequiredService<EventBus>();
        hotReloader.CreateServices(factory, _logger, network, mapData, mapService, handlerRegistry, playerSession, worldState, eventBus);

        // 4. 启动游戏逻辑调度器
        var gameLoop = _sp.GetRequiredService<IGameLoopScheduler>();
        await gameLoop.StartAsync(_cts.Token);
        _logger.LogInformation("GameLoopScheduler started");

        // 断线/踢线：在逻辑线程清理玩家世界状态与在线标记（修复幽灵玩家 / 空间索引格子泄漏）
        network.OnPlayerDisconnected = (accountId, serverId) =>
        {
            var mapName = worldState.GetEntityMapName(accountId);
            if (mapName != null)
                mapService.PlayerLeave(accountId, mapName);
            playerSession.SetOffline(accountId);
            _logger.LogInformation("[Gateway] 玩家下线清理: account={AccountId} map={Map}", accountId, mapName ?? "(不在图)");
        };

        // 5. 注册消息路由
        var router = _sp.GetRequiredService<MessageRouter>();
        var gateway = _sp.GetRequiredService<GatewayService>();
        var loginService = _sp.GetRequiredService<LoginService>();
        loginService.RegisterRoutes(router, gateway);
        handlerRegistry.RegisterAll(router);
        _logger.LogInformation("Message routes registered");

        // 5. 订阅碰撞事件到战斗系统
        eventBus.On("CollisionDetected", (data) =>
        {
            var (entityA, entityB, mapName) = ((long entityA, long entityB, string mapName))data!;
            var maps = mapService.GetMapsSnapshot();
            mapService.RefreshCombatPositionsForSnapshot(maps);
            hotReloader.CombatService?.OnCollision(entityA, entityB, maps);
        });

        // 订阅 NPC 碰撞事件 → 发送 NpcInteractNotify
        eventBus.On("NpcCollisionDetected", (data) =>
        {
            var (playerId, npcInstanceId, npcMapName) = ((long playerId, long npcInstanceId, string npcMapName))data!;
            var ws = _sp.GetRequiredService<WorldState>();
            var mapInst = ws.GetMapState(npcMapName);
            if (mapInst == null || !mapInst.Npcs.TryGetValue(npcInstanceId, out var npc)) return;

            var notify = new PGame.NpcInteractNotify
            {
                NpcInstanceId = (ulong)npcInstanceId,
                NpcName = npc.Name,
                NpcType = npc.NpcType,
            };
            // 找到该玩家的 serverId
            var player = mapInst.Players.GetValueOrDefault(playerId);
            if (player == null) return;
            network.SendToAccount(playerId, player.ServerId, (int)PProtocol.MessageId.GameNpcInteractNotify, notify.ToByteArray());
        });

        // 订阅 NPC 挑战事件 → 建立战斗关系（和碰撞触发一样）
        eventBus.On("NpcCombatTriggered", (data) =>
        {
            var (playerId, npcInstanceId, combatMapName) = ((long playerId, long npcInstanceId, string combatMapName))data!;
            var maps = mapService.GetMapsSnapshot();
            mapService.RefreshCombatPositionsForSnapshot(maps);
            hotReloader.CombatService?.OnCollision(playerId, npcInstanceId, maps);
        });

        // 初始化怪物
        hotReloader.MonsterService?.Init();

        // 初始化 NPC
        var npcManager = factory.InitNpcs(worldState);
        hotReloader.NpcManager = npcManager;
        playerSession.NpcManager = npcManager;
        _logger.LogInformation("Combat & Monster & NPC initialized");

        // 6. 启动游戏 tick 定时器（定时器线程仅负责唤醒，实际逻辑投递到 GameLoopScheduler）
        _ = MonsterTickLoop(hotReloader, gameLoop, _cts.Token);
        _ = CombatTickLoop(hotReloader, mapService, gameLoop, _cts.Token);
        _ = DropTickLoop(hotReloader, gameLoop, _cts.Token);
        _ = PlayerAutoSaveLoop(playerSession, gameLoop, _cts.Token);
        _ = HotReloadCommandLoop(hotReloader, _logger, network, mapData, mapService, handlerRegistry, playerSession, router, worldState, eventBus, _cts.Token);
        _logger.LogInformation("Game tick loops started");

        // 7. 启动 Gateway
        _ = gateway.StartAsync(_cts.Token);
        _logger.LogInformation("======== Game Server Ready ========");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        var gameLoop = _sp.GetService<IGameLoopScheduler>();
        if (gameLoop != null)
            await gameLoop.StopAsync(cancellationToken);
        _logger.LogInformation("======== Game Server Stopped ========");
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

    private static async Task MonsterTickLoop(HotReloader hotReloader, IGameLoopScheduler gameLoop, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(GameConstants.MonsterAiTickMs));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var monsterService = hotReloader.MonsterService;
            if (monsterService != null)
                gameLoop.Enqueue(() => monsterService.Tick());
        }
    }

    private static async Task CombatTickLoop(
        HotReloader hotReloader,
        MapService mapService,
        IGameLoopScheduler gameLoop,
        CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(GameConstants.CombatTickMs));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var combatService = hotReloader.CombatService;
            var monsterService = hotReloader.MonsterService;
            if (combatService != null)
            {
                gameLoop.Enqueue(() =>
                {
                    var maps = mapService.GetMapsSnapshot();
                    mapService.RefreshCombatPositionsForSnapshot(maps);
                    combatService.Tick(0.1, maps, monsterService as IMonsterRegistry);
                    // 非战斗状态的 buff 过期检查
                    mapService.TickOutOfCombatBuffs(maps);
                });
            }
        }
    }

    private static async Task DropTickLoop(HotReloader hotReloader, IGameLoopScheduler gameLoop, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(GameConstants.CombatTickMs));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var dropService = hotReloader.DropService;
            if (dropService != null)
                gameLoop.Enqueue(() => dropService.Tick(0.1f));
        }
    }

    private static async Task PlayerAutoSaveLoop(
        PlayerSessionManager playerSession,
        IGameLoopScheduler gameLoop,
        CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                var players = playerSession.OnlinePlayers;
                if (players.Count == 0) continue;

                // 在 GameLoop 线程读取 Role 字段生成快照，避免与游戏逻辑线程竞争
                var snapshots = await gameLoop.Enqueue(() =>
                {
                    var list = new List<(long AccountId, long RoleId, RoleSaveSnapshot Snapshot)>();
                    foreach (var (accountId, role) in players)
                    {
                        list.Add((accountId, role.RoleId, new RoleSaveSnapshot(
                            role.Hp, role.MaxHp, role.Mp, role.MaxMp,
                            role.Patk, role.Matk, role.Pdef, role.Mdef,
                            role.MpRegen, role.MoveSpeedMs,
                            role.GridX, role.GridY,
                            role.CurrentMap,
                            role.Level, role.Exp,
                            role.Job,
                            role.PreferredSkillId,
                            new List<int>(role.LearnedSkills),
                            new List<int>(role.EquippedSkills))));
                    }
                    return list;
                });

                // 在后台线程批量写 MongoDB，不阻塞 GameLoop
                _ = Task.Run(async () =>
                {
                    int saved = 0;
                    foreach (var (accountId, roleId, snapshot) in snapshots)
                    {
                        try
                        {
                            await playerSession.Roles.Update(roleId, u =>
                                u.Set(r => r.Hp, snapshot.Hp)
                                 .Set(r => r.MaxHp, snapshot.MaxHp)
                                 .Set(r => r.Mp, snapshot.Mp)
                                 .Set(r => r.MaxMp, snapshot.MaxMp)
                                 .Set(r => r.Patk, snapshot.Patk)
                                 .Set(r => r.Matk, snapshot.Matk)
                                 .Set(r => r.Pdef, snapshot.Pdef)
                                 .Set(r => r.Mdef, snapshot.Mdef)
                                 .Set(r => r.MpRegen, snapshot.MpRegen)
                                 .Set(r => r.MoveSpeedMs, snapshot.MoveSpeedMs)
                                 .Set(r => r.GridX, snapshot.GridX)
                                 .Set(r => r.GridY, snapshot.GridY)
                                 .Set(r => r.CurrentMap, snapshot.CurrentMap)
                                 .Set(r => r.Level, snapshot.Level)
                                 .Set(r => r.Exp, snapshot.Exp)
                                 .Set(r => r.Job, snapshot.Job)
                                 .Set(r => r.PreferredSkillId, snapshot.PreferredSkillId)
                                 .Set(r => r.LearnedSkills, snapshot.LearnedSkills)
                                 .Set(r => r.EquippedSkills, snapshot.EquippedSkills));
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AutoSave] Failed for role {roleId}: {ex.Message}");
                        }
                    }
                    Console.WriteLine($"[AutoSave] Saved {saved}/{snapshots.Count} online players");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoSave] Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 控制台热更命令监听 — 输入 "reload" 触发热更
    /// </summary>
    private static Task HotReloadCommandLoop(
        HotReloader hotReloader,
        Microsoft.Extensions.Logging.ILogger logger,
        GatewayService network,
        MapDataProvider mapData,
        MapService mapService,
        MessageHandlerRegistry handlerRegistry,
        PlayerSessionManager playerSession,
        MessageRouter router,
        WorldState worldState,
        EventBus eventBus,
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
                        hotReloader.Reload(logger, network, mapData, mapService, handlerRegistry, playerSession, router, worldState, eventBus);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[HotReload] Failed");
                    }
                }
            }
        }, ct);
        return Task.CompletedTask;
    }
}
