using System.Reflection;
using GameServer.GameLogic;
using GameServer.Services.Core;
using GameServer.Services.Player;
using GameServer.Services.Player.Handlers;
using GameServer.Tables;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameServer.Tests;

public class GameLogicFactoryTests
{
    [Fact]
    public void CreateHandler_ResolvesGenericLoggerDependency()
    {
        using var loggerFactory = LoggerFactory.Create(builder => { });
        var factory = new GameLogicFactory(loggerFactory, new LubanTableLoader(loggerFactory.CreateLogger<LubanTableLoader>()));
        var dependencies = new Dictionary<Type, Func<object>>
        {
            [typeof(PlayerSessionManager)] = static () => null!,
            [typeof(INetworkSender)] = static () => null!,
        };

        var createHandler = typeof(GameLogicFactory).GetMethod(
            "CreateHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(createHandler);

        var handler = createHandler!.Invoke(factory, new object[] { typeof(MoveStartHandler), dependencies });

        Assert.IsType<MoveStartHandler>(handler);
    }
}
