namespace ClinetCSharp
{
    /// <summary>
    /// 建筑类型常量 — 决定建筑配置 ID 与建筑 UID 的区间。
    /// </summary>
    public static class BuildingType
    {
        public const int House = 1;
        public const int Shop = 2;
        public const int Well = 3;       // 水井，城镇点缀
        public const int Farm = 4;       // 农田/谷仓，城镇边缘
        public const int Tavern = 5;     // 酒馆/地标，城镇中心
        public const int SpawnPoint = 6; // 出生点，仅编辑器可见
        public const int Portal = 7;     // 共享传送门
        public const int Water = 8;      // 水域/水塘，作为建筑装饰
        public const int Rock = 9;       // 岩石，作为建筑装饰
        public const int Tree = 10;      // 树木，作为地形装饰
        public const int Grass = 11;     // 草地，作为地形装饰

        public const int ConfigIdMultiplier = 10000;
        public const int UidMultiplier = 100000;

        public static bool IsValid(int type) => type is House or Shop or Well or Farm or Tavern or SpawnPoint or Portal or Water or Rock or Tree or Grass;

        public static int GetConfigBaseId(int type) => type * ConfigIdMultiplier;

        public static int GetUidBaseId(int type) => type * UidMultiplier;

        public static int GetTypeFromConfigId(int configId) => configId / ConfigIdMultiplier;

        public static int GetTypeFromUid(int uid) => uid / UidMultiplier;

        /// <summary>建筑类型 → 中文显示名</summary>
        public static string GetDisplayName(int type) => type switch
        {
            House => "房舍",
            Shop => "商店",
            Well => "水井",
            Farm => "农田",
            Tavern => "酒馆",
            SpawnPoint => "出生点",
            Portal => "共享传送门",
            Water => "水",
            Rock => "岩石",
            Tree => "树",
            Grass => "草地",
            _ => "未知",
        };

        /// <summary>获取所有合法建筑类型（按 ID 升序）</summary>
        public static int[] GetAllTypes() => new[] { House, Shop, Well, Farm, Tavern, SpawnPoint, Portal, Water, Rock, Tree, Grass };
    }
}
