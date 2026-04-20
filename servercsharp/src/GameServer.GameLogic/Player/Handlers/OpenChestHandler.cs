using GameServer.Common.Models;
using GameServer.Common.Net;
using GameServer.Database.Models;
using GameServer.Services.Core;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PCommon = global::Common;
using PGame = global::Game;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)global::Protocol.MessageId.GameOpenChestReq)]
public class OpenChestHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;

    public OpenChestHandler(PlayerSessionManager session) => _session = session;

    public async Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var claims = ctx.Claims!;
        if (!_session.TryGetPlayer(claims.AccountId, out var player))
            return MakeError(PCommon.ErrorCode.Unauthorized);

        var req = PGame.OpenChestRequest.Parser.ParseFrom(data);
        var instanceId = (long)req.ChestId;
        if (instanceId == 0)
            return MakeError(PCommon.ErrorCode.InvalidRequest);

        var (mapId, entityType, _) = InstanceId.Parse(instanceId);
        if (entityType != MapEntityType.Chest)
            return MakeError(PCommon.ErrorCode.InvalidRequest);

        var mapChests = await _session.Chests.GetGmChestsByMapAndType(mapId, (int)MapEntityType.Chest);
        int? chestX = null, chestY = null;
        bool found = false;
        foreach (var c in mapChests)
        {
            if (c.Id == instanceId)
            {
                chestX = c.X;
                chestY = c.Y;
                found = true;
                break;
            }
        }
        if (!found)
            return MakeError(PCommon.ErrorCode.NotFound);

        if (Math.Abs(player.GridX - chestX!.Value) + Math.Abs(player.GridY - chestY!.Value) > 1)
            return MakeError(PCommon.ErrorCode.Forbidden);

        var roleId = player.RoleId;
        if (await _session.Chests.IsOpened(roleId, instanceId))
            return new PGame.OpenChestResponse { Code = PCommon.ErrorCode.InvalidRequest, Message = "chest already opened" }.ToByteArray();

        var rewards = RewardParser.Parse("");
        foreach (var item in rewards)
            await _session.Inventory.AddItem(roleId, item.ItemId, item.Count);

        await _session.Chests.MarkOpened(roleId, instanceId);
        _session.Logger.LogInformation("OpenChest: roleId={RoleId} instanceId={InstanceId}", roleId, instanceId);

        var rsp = new PGame.OpenChestResponse { Code = PCommon.ErrorCode.Success, Message = "" };
        foreach (var r in rewards)
            rsp.Items.Add(new PGame.ItemInfo { ItemId = (uint)r.ItemId, Count = (uint)r.Count });
        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code)
        => new PCommon.Response { Code = code, Message = "" }.ToByteArray();
}
