using GameServer.Common.Models;
using GameServer.Database.Models;
using GameServer.Database.Repositories;
using GameServer.Services.Core;
using PGame = global::Game;

namespace GameServer.Services.Player;

/// <summary>
/// Role ↔ Protobuf 转换辅助
/// </summary>
public static class PlayerProtoMapper
{
    /// <summary>角色属性枚举约定</summary>
    public static class RoleAttrs
    {
        public const uint Hp = 1, MaxHp = 2, Mp = 3, MaxMp = 4;
        public const uint Agility = 5, PAtk = 6, MAtk = 7, PDef = 8, MDef = 9;
        public const uint MoveSpeed = 10;

        public static readonly (uint key, string name)[] All =
        {
            (Hp, "hp"), (MaxHp, "max_hp"), (Mp, "mp"), (MaxMp, "max_mp"),
            (Agility, "agility"), (PAtk, "patk"), (MAtk, "matk"), (PDef, "pdef"), (MDef, "mdef"),
            (MoveSpeed, "move_speed"),
        };

        public static uint? NameToKey(string name)
        {
            foreach (var (k, n) in All)
                if (n == name) return k;
            return null;
        }
    }

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
            CurrentMap = role.CurrentMap,
            GridX = role.GridX,
            GridY = role.GridY,
        };

        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.Hp, Value = role.Hp });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MaxHp, Value = role.MaxHp });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.Mp, Value = role.Mp });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MaxMp, Value = role.MaxMp });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.Agility, Value = role.Agility });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.PAtk, Value = role.Patk });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MAtk, Value = role.Matk });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.PDef, Value = role.Pdef });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MDef, Value = role.Mdef });
        info.Attrs.Add(new PGame.AttrItem { Key = RoleAttrs.MoveSpeed, Value = role.MoveSpeedMs > 0 ? role.MoveSpeedMs : GameConstants.BaseMoveSpeedMs });

        return info;
    }

    public static async Task<List<PGame.ItemInfo>> BuildItemsProto(InventoryRepository inventory, long roleId)
    {
        var dbItems = await inventory.GetByRole(roleId);
        var result = new List<PGame.ItemInfo>();
        foreach (var item in dbItems)
            result.Add(new PGame.ItemInfo { ItemId = (uint)item.ItemId, Count = (uint)item.Count });
        return result;
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
