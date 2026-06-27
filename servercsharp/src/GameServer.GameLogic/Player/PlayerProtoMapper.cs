using GameServer.Common;
using GameServer.Common.Models;
using GameServer.Database.Models;
using GameServer.Database.Repositories;
using GameServer.GameLogic.Inventory;
using GameServer.Services.Core;
using GameServer.Tables;
using PGame = global::Game;

namespace GameServer.Services.Player;

/// <summary>
/// Role ↔ Protobuf 转换辅助
/// </summary>
public static class PlayerProtoMapper
{
    // RoleAttrs 已移至 GameServer.Common.RoleAttrs

    public static PGame.FullRoleInfo BuildRoleInfo(Role role, int now)
    {
        var info = new PGame.FullRoleInfo
        {
            RoleId = (uint)role.RoleId,
            RoleName = role.RoleName,
            Level = (uint)role.Level,
            Exp = (uint)role.Exp,
            AvatarId = (uint)role.AvatarId,
            Gold = (uint)role.Gold,
            Diamond = (uint)role.Diamond,
            TotalPower = (uint)role.TotalPower,
            VipLevel = (uint)role.VipLevel,
            CreateTime = (uint)role.CreateTime,
            LastLoginTime = (uint)now,
            Job = role.Job,
            Title = role.Title,
            Status = role.Status,
            CurrentMap = MapNameNormalizer.Normalize(role.CurrentMap),
            GridX = role.GridX,
            GridY = role.GridY,
        };

        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.Hp, Value = role.Hp });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MaxHp, Value = role.MaxHp });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.Mp, Value = role.Mp });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MaxMp, Value = role.MaxMp });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.PAtk, Value = role.Patk });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MAtk, Value = role.Matk });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.PDef, Value = role.Pdef });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MDef, Value = role.Mdef });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MoveSpeed, Value = role.MoveSpeedMs > 0 ? role.MoveSpeedMs : GameConstants.BaseMoveSpeedMs });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MpRegen, Value = role.MpRegen });

        foreach (var sid in role.LearnedSkills)  info.LearnedSkills.Add((uint)sid);
        foreach (var sid in role.EquippedSkills) info.EquippedSkills.Add((uint)sid);

        return info;
    }

    public static async Task<List<PGame.ItemInfo>> BuildItemsProto(InventoryRepository inventory, Role role, LubanTableLoader tables)
    {
        var dbItems = await inventory.GetByRole(role.RoleId);
        return InventoryHelper.BuildItemsProto(dbItems, tables, role.InventoryOrder);
    }

    public static async Task<PGame.ChestInfo> BuildChestsProto(long roleId, int mapId)
    {
        return await Task.FromResult(new PGame.ChestInfo());
    }

    public static List<RewardParser.RewardItem> ParseInitItems(string str)
    {
        var items = new List<RewardParser.RewardItem>();
        if (string.IsNullOrEmpty(str)) return items;
        foreach (var pair in str.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out var id) && int.TryParse(parts[1], out var count))
                items.Add(new RewardParser.RewardItem(id, count));
        }
        return items;
    }
}
