namespace GameServer.Common;

/// <summary>
/// 角色属性枚举约定 — 和 Luban EAttr 枚举对齐
/// 新增属性：在此加常量 + 在 All 数组加一行
/// </summary>
public static class RoleAttrs
{
    public const int Hp = 1, MaxHp = 2, Mp = 3, MaxMp = 4;
    public const int PAtk = 6, MAtk = 7, PDef = 8, MDef = 9;
    public const int MoveSpeed = 10, MpRegen = 11;

    public static readonly (int key, string name)[] All =
    {
        (Hp, "hp"), (MaxHp, "max_hp"), (Mp, "mp"), (MaxMp, "max_mp"),
        (PAtk, "patk"), (MAtk, "matk"), (PDef, "pdef"), (MDef, "mdef"),
        (MoveSpeed, "move_speed"), (MpRegen, "mp_regen"),
    };

    public static int? NameToKey(string name)
    {
        foreach (var (k, n) in All)
            if (n == name) return k;
        return null;
    }
}
