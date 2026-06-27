using GameServer.Common.Events;
using GameServer.Common.Config;
using GameServer.Services.Map;
using GameServer.Services.Player;
using GameServer.Services.World;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Core;

/// <summary>
/// 游戏逻辑工厂 — 热更 DLL 的唯一入口
/// 宿主通过此接口创建所有逻辑对象，不直接引用具体类
/// </summary>
public interface IGameLogicFactory
{
    /// <summary>创建战斗服务</summary>
    ICombatService CreateCombatService(ILogger logger, INetworkSender network, MapDataProvider mapData);

    /// <summary>创建怪物 AI 服务</summary>
    IMonsterAiService CreateMonsterAiService(MapDataProvider mapData, MapService mapService, INetworkSender network);

    /// <summary>注册消息处理器</summary>
    void RegisterMessageHandlers(
        MessageHandlerRegistry registry,
        PlayerSessionManager session,
        INetworkSender network,
        MapDataProvider mapData,
        IMonsterAiService monsterAi,
        WorldState worldState,
        EventBus eventBus,
        IDropService dropService);

    /// <summary>绑定死亡回调到战斗服务</summary>
    void BindDeathHandler(ICombatService combatService, MapService mapService, MapDataProvider mapData, PlayerSessionManager session, INetworkSender network);

    /// <summary>绑定怪物注册接口到战斗服务（用于伤害通知怪物进入战斗状态）</summary>
    void BindMonsterRegistry(ICombatService combatService, IMonsterAiService monsterAi);

    /// <summary>绑定升级服务到怪物死亡回调</summary>
    void BindLevelUpService(ICombatService combatService, IMonsterAiService monsterAi, MapService mapService, PlayerSessionManager session, INetworkSender network);

    /// <summary>创建掉落管理器（尚未绑定到怪物死亡回调）</summary>
    IDropService CreateDropManager(PlayerSessionManager session, INetworkSender network);

    /// <summary>绑定掉落管理器：设置网络广播回调并接入怪物死亡事件</summary>
    void BindDropManager(IDropService dropService, IMonsterAiService monsterAi, MapService mapService);

    /// <summary>初始化 NPC 并返回 NPC 管理器</summary>
    INpcManager InitNpcs(WorldState worldState);
}

/// <summary>
/// NPC 管理器接口 — 宿主通过此接口查询 NPC 数据
/// </summary>
public interface INpcManager
{
    List<MapNpcState> GetNpcsOnMap(string mapName);
}
