using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GameServer.Common.Net;
using GameServer.Common.Security;
using GameServer.Services.Core;
using GameServer.Services.Player;
using GameServer.Services.World;
using Google.Protobuf;
using PCommon = Common;
using PGame = Game;
using PProtocol = Protocol;

namespace GameServer.HttpApi;

/// <summary>
/// Lightweight HTTP API server for the admin panel.
/// Uses ASP.NET Core Minimal API + Kestrel on a separate port.
/// </summary>
public class HttpApiServer
{
    private readonly int _port;
    private readonly IServiceProvider _sp;
    private readonly ILogger<HttpApiServer> _logger;
    private WebApplication? _app;

    public HttpApiServer(IConfiguration config, IServiceProvider sp, ILogger<HttpApiServer> logger)
    {
        _port = int.Parse(config["HttpApi:Port"] ?? "8890");
        _sp = sp;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://0.0.0.0:{_port}");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddCors();

        _app = builder.Build();
        _app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        MapEndpoints();

        _logger.LogInformation("HttpApiServer listening on port {Port}", _port);
        await _app.RunAsync();
    }

    public async Task StopAsync()
    {
        if (_app != null)
            await _app.StopAsync();
    }

    private void MapEndpoints()
    {
        if (_app == null) return;

        var api = _app.MapGroup("/api");

        api.MapGet("/status", () =>
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var playerSession = _sp.GetService<PlayerSessionManager>();

                return Results.Ok(new
                {
                    cpu = Math.Round(GetCpuUsage(), 1),
                    memory = (int)(process.WorkingSet64 / (1024 * 1024)),
                    uptime = (int)(DateTime.Now - process.StartTime).TotalSeconds,
                    onlinePlayers = playerSession?.OnlinePlayers.Count ?? 0,
                    gatewayPort = 8889,
                    gameTickMs = 0,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get status");
                return Results.Problem("Failed to get server status");
            }
        });

        api.MapGet("/players", () =>
        {
            try
            {
                var playerSession = _sp.GetService<PlayerSessionManager>();
                if (playerSession == null) return Results.Ok(Array.Empty<object>());

                var players = playerSession.OnlinePlayers.Values.Select(r => new
                {
                    accountId = r.AccountId,
                    roleId = r.RoleId,
                    roleName = r.RoleName,
                    level = r.Level,
                    job = r.Job,
                    currentMap = r.CurrentMap,
                    gridX = r.GridX,
                    gridY = r.GridY,
                }).ToArray();

                return Results.Ok(players);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get players");
                return Results.Problem("Failed to get player list");
            }
        });

        api.MapGet("/maps", () =>
        {
            try
            {
                var worldState = _sp.GetService<WorldState>();
                if (worldState == null) return Results.Ok(Array.Empty<object>());

                var maps = worldState.GetAllMaps().Select(kvp =>
                {
                    var state = kvp.Value;
                    return new
                    {
                        mapName = kvp.Key,
                        playerCount = state.Players.Count,
                        monsterCount = state.Monsters.Count,
                        npcCount = state.Npcs.Count,
                    };
                }).ToArray();

                return Results.Ok(maps);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get maps");
                return Results.Problem("Failed to get map entities");
            }
        });

        api.MapPost("/gm", async (HttpRequest request) =>
        {
            try
            {
                var body = await request.ReadFromJsonAsync<GmRequest>();
                if (body == null || string.IsNullOrWhiteSpace(body.Command))
                    return Results.BadRequest(new { success = false, message = "Command is required" });

                var playerSession = _sp.GetService<PlayerSessionManager>();
                var router = _sp.GetService<MessageRouter>();
                var gameLoop = _sp.GetService<IGameLoopScheduler>();

                if (playerSession == null) return Results.Problem("Player session not available");
                if (router == null) return Results.Problem("Message router not available");
                if (gameLoop == null) return Results.Problem("Game loop not available");

                // Find target player (use first online player if no target specified)
                var targetPlayer = body.TargetPlayerId.HasValue
                    ? playerSession.OnlinePlayers.GetValueOrDefault(body.TargetPlayerId.Value)
                    : playerSession.OnlinePlayers.Values.FirstOrDefault();

                if (targetPlayer == null)
                    return Results.Ok(new { success = false, message = "No online player found to execute GM command" });

                var req = new PGame.GmCommandRequest { Command = body.Command };
                var ctx = new MessageContext
                {
                    ConnId = 0,
                    Session = 0,
                    Token = "http-api",
                    AccountId = targetPlayer.AccountId,
                    ServerId = targetPlayer.ServerId,
                    Claims = new GatewayTokenClaims
                    {
                        AccountId = targetPlayer.AccountId,
                        ServerId = targetPlayer.ServerId,
                    },
                };

                var result = await gameLoop.Enqueue(async () =>
                {
                    try
                    {
                        var response = await router.Dispatch(
                            (int)PProtocol.MessageId.GameGmReq,
                            ctx,
                            req.ToByteArray());

                        if (response != null)
                        {
                            var parsed = PGame.GmCommandResponse.Parser.ParseFrom(response);
                            return new { success = parsed.Code == PCommon.ErrorCode.Success, message = parsed.Message ?? "Done" };
                        }
                        return new { success = false, message = "No response from GM handler" };
                    }
                    catch (Exception ex)
                    {
                        return new { success = false, message = ex.Message };
                    }
                });

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to execute GM command");
                return Results.Problem("Failed to execute GM command");
            }
        });

        api.MapGet("/health", () => Results.Ok(new { status = "ok" }));
    }

    private static double GetCpuUsage()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpu = process.TotalProcessorTime;
            Thread.Sleep(100);
            var endTime = DateTime.UtcNow;
            var endCpu = process.TotalProcessorTime;
            var cpuUsedMs = (endCpu - startCpu).TotalMilliseconds;
            var elapsedMs = (endTime - startTime).TotalMilliseconds;
            return (cpuUsedMs / (Environment.ProcessorCount * elapsedMs)) * 100;
        }
        catch
        {
            return 0;
        }
    }
}

public record GmRequest(string Command, int? TargetPlayerId);