using GameServer.Common.Events;
using GameServer.Common.Net;
using GameServer.Services.Core;
using GameServer.Services.Map.Combat;
using GameServer.Services.Player;
using GameServer.Services.World;
using Google.Protobuf;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.GameLogic.Npc.Handlers;

/// <summary>
/// NPC 挑战请求处理 — 将 NPC 加入战斗关系
/// </summary>
[HandlesMessage((int)PProtocol.MessageId.GameNpcCombatReq)]
public class NpcCombatHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly INetworkSender _network;
    private readonly WorldState _worldState;
    private readonly EventBus _eventBus;

    public NpcCombatHandler(PlayerSessionManager session, INetworkSender network, WorldState worldState, EventBus eventBus)
    {
        _session = session;
        _network = network;
        _worldState = worldState;
        _eventBus = eventBus;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return MakeError(PCommon.ErrorCode.Unauthorized);

        var req = PGame.NpcCombatRequest.Parser.ParseFrom(data);
        long npcInstanceId = (long)req.NpcInstanceId;

        // 找到玩家所在地图
        var entityPos = _worldState.FindEntityPosition(claims.AccountId);
        string? mapName = entityPos.mapName;
        if (mapName == null)
            return MakeError(PCommon.ErrorCode.Unauthorized);

        // 找到 NPC
        var map = _worldState.GetMapState(mapName);
        if (map == null || !map.Npcs.TryGetValue(npcInstanceId, out var npc))
            return MakeError(PCommon.ErrorCode.InvalidRequest);

        // NPC 已经在战斗中
        if (npc.InCombat)
            return MakeError(PCommon.ErrorCode.InvalidRequest);

        // 初始化 NPC 战斗属性（如果还没设过）
        if (npc.MaxHp <= 0)
        {
            // TODO: 从配置表读取 NPC 战斗属性
            npc.MaxHp = CombatConstants.DefaultNpcCombat.MaxHp;
            npc.Hp = CombatConstants.DefaultNpcCombat.Hp;
            npc.MaxMp = CombatConstants.DefaultNpcCombat.MaxMp;
            npc.Mp = CombatConstants.DefaultNpcCombat.Mp;
            npc.Patk = CombatConstants.DefaultNpcCombat.Patk;
            npc.Matk = CombatConstants.DefaultNpcCombat.Matk;
            npc.Pdef = CombatConstants.DefaultNpcCombat.Pdef;
            npc.Mdef = CombatConstants.DefaultNpcCombat.Mdef;
        }
        else
        {
            npc.Hp = npc.MaxHp;
            npc.Mp = npc.MaxMp;
        }

        npc.InCombat = true;

        // 通过事件总线触发战斗关系建立
        _eventBus.Emit("NpcCombatTriggered", (claims.AccountId, npcInstanceId, mapName));

        var rsp = new PGame.NpcCombatResponse
        {
            Code = PCommon.ErrorCode.Success,
            NpcInstanceId = req.NpcInstanceId,
        };
        return rsp.ToByteArray();
    }

    private byte[] MakeError(PCommon.ErrorCode code)
    {
        var rsp = new PGame.NpcCombatResponse { Code = code };
        return rsp.ToByteArray();
    }
}