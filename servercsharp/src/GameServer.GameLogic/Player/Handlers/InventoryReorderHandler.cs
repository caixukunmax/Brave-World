using GameServer.Common.Net;
using GameServer.GameLogic.Inventory;
using GameServer.Services.Core;
using GameServer.Services.Player;
using GameServer.Tables;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PCommon = global::Common;
using PGame = global::Game;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)global::Protocol.MessageId.GameInventoryReorderReq)]
public class InventoryReorderHandler : IMessageHandler
{
    private readonly ILogger<InventoryReorderHandler> _logger;
    private readonly PlayerSessionManager _session;
    private readonly LubanTableLoader _tables;

    public InventoryReorderHandler(
        ILogger<InventoryReorderHandler> logger,
        PlayerSessionManager session,
        LubanTableLoader tables)
    {
        _logger = logger;
        _session = session;
        _tables = tables;
    }

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return MakeError(PCommon.ErrorCode.Unauthorized, "player not found");

        var req = PGame.InventoryReorderRequest.Parser.ParseFrom(data);
        var orderedIds = req.OrderedItemIds.Select(id => (int)id).ToList();

        var dbItems = await _session.Inventory.GetByRole(player.RoleId);

        // 校验：order 中不能包含玩家没有的 item_id
        var ownedIds = new HashSet<int>(dbItems.Select(i => i.ItemId));
        foreach (var id in orderedIds)
        {
            if (!ownedIds.Contains(id))
                return MakeError(PCommon.ErrorCode.InvalidRequest, $"invalid item id {id}");
        }

        // 保存新顺序
        player.InventoryOrder = orderedIds;
        await _session.Roles.UpdateInventoryOrder(player.RoleId, orderedIds);

        // 返回排序后的完整背包
        var items = await PlayerProtoMapper.BuildItemsProto(_session.Inventory, player, _tables);
        var rsp = new PGame.InventoryReorderResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "",
        };
        foreach (var item in items) rsp.Items.Add(item);
        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code, string message)
        => new PGame.InventoryReorderResponse { Code = code, Message = message }.ToByteArray();
}
