using GameServer.Common.Net;
using GameServer.Services.Core;
using Google.Protobuf;
using PCommon = global::Common;
using PGame = global::Game;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)global::Protocol.MessageId.GameDropItemReq)]
public class DropItemHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;

    public DropItemHandler(PlayerSessionManager session) => _session = session;

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return MakeError(PCommon.ErrorCode.Unauthorized);

        var req = PGame.DropItemRequest.Parser.ParseFrom(data);
        var itemId = (int)req.ItemId;
        var count = (int)req.Count;
        if (itemId == 0 || count == 0)
            return MakeError(PCommon.ErrorCode.InvalidRequest);

        var ok = await _session.Inventory.RemoveItem(player.RoleId, itemId, count);
        if (!ok)
            return new PGame.DropItemResponse { Code = PCommon.ErrorCode.NotFound, Message = "item not enough" }.ToByteArray();

        var rsp = new PGame.DropItemResponse { Code = PCommon.ErrorCode.Success, Message = "" };
        var items = await PlayerProtoMapper.BuildItemsProto(_session.Inventory, player.RoleId);
        foreach (var item in items) rsp.Items.Add(item);
        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code)
        => new PCommon.Response { Code = code, Message = "" }.ToByteArray();
}
