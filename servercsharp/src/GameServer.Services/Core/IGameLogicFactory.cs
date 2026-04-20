using GameServer.Common.Config;
using GameServer.Services.Map;
using GameServer.Services.Player;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Core;

/// <summary>
/// 游戏逻辑工厂 — 热更 DLL 的唯一入口
/// 宿主通过此接口创建所有逻辑对象，不直接引用具体类
/// </summary>
public interface IGameLogicFactory
{
    /// <summary>创建战斗服务</summary>
    ICombatService CreateCombatService(ILogger logger, INetworkSender network);

    /// <summary>创建怪物 AI 服务</summary>
    IMonsterAiService CreateMonsterAiService(MapDataProvider mapData, MapService mapService, INetworkSender network);

    /// <summary>注册消息处理器</summary>
    void RegisterMessageHandlers(MessageHandlerRegistry registry, PlayerSessionManager session, INetworkSender network, MapDataProvider mapData, IMonsterAiService monsterAi);
}
