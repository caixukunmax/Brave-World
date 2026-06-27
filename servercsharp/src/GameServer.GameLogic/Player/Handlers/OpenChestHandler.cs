using GameServer.Common.Models;
using GameServer.Common.Net;
using GameServer.Database.Models;
using GameServer.GameLogic.Inventory;
using GameServer.Services.Core;
using GameServer.Tables;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PCommon = global::Common;
using PGame = global::Game;

namespace GameServer.Services.Player.Handlers;

[HandlesMessage((int)global::Protocol.MessageId.GameOpenChestReq)]
public class OpenChestHandler : IMessageHandler
{
    private readonly PlayerSessionManager _session;
    private readonly LubanTableLoader _tables;

    public OpenChestHandler(PlayerSessionManager session, LubanTableLoader tables)
    {
        _session = session;
        _tables = tables;
    }

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
        var dbItems = await _session.Inventory.GetByRole(roleId);
        var actualRewards = new List<(int itemId, int count)>();

        foreach (var reward in rewards)
        {
            var (added, remaining) = InventoryHelper.CalculatePickupCapacity(dbItems, _tables, reward.ItemId, reward.Count);
            if (added > 0)
            {
                await _session.Inventory.AddItem(roleId, reward.ItemId, added);
                actualRewards.Add((reward.ItemId, added));

                // 更新内存中的聚合数据，供后续奖励计算更准确
                var existing = dbItems.FirstOrDefault(i => i.ItemId == reward.ItemId);
                if (existing != null)
                    existing.Count += added;
                else
                    dbItems.Add(new InventoryItem { RoleId = roleId, ItemId = reward.ItemId, Count = added });
            }
        }

        await _session.Chests.MarkOpened(roleId, instanceId);
        _session.Logger.LogInformation("OpenChest: roleId={RoleId} instanceId={InstanceId} rewards={RewardCount}", roleId, instanceId, actualRewards.Count);

        var rsp = new PGame.OpenChestResponse { Code = PCommon.ErrorCode.Success, Message = "" };
        foreach (var r in actualRewards)
            rsp.Items.Add(new PGame.ItemInfo { ItemId = (uint)r.itemId, Count = (uint)r.count });
        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code)
        => new PCommon.Response { Code = code, Message = "" }.ToByteArray();
}
