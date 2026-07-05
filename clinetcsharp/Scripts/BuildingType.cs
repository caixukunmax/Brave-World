namespace ClinetCSharp
{
    /// <summary>
    /// 建筑类型常量 — 决定建筑配置 ID 与建筑 UID 的区间。
    /// </summary>
    public static class BuildingType
    {
        public const int House = 1;
        public const int Shop = 2;

        public const int ConfigIdMultiplier = 10000;
        public const int UidMultiplier = 100000;

        public static bool IsValid(int type) => type == House || type == Shop;

        public static int GetConfigBaseId(int type) => type * ConfigIdMultiplier;

        public static int GetUidBaseId(int type) => type * UidMultiplier;

        public static int GetTypeFromConfigId(int configId) => configId / ConfigIdMultiplier;

        public static int GetTypeFromUid(int uid) => uid / UidMultiplier;
    }
}
