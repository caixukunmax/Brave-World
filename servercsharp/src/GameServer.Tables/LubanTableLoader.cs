using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameServer.Tables;

/// <summary>
/// Luban 配置表加载器 — 从 JSON 数据文件加载所有表
/// 数据文件由 tables/scripts/build_tables.ts 从 xlsx 生成
/// </summary>
public class LubanTableLoader
{
    private readonly ILogger<LubanTableLoader> _logger;

    public Dictionary<int, MonsterRow> Monsters { get; private set; } = new();
    public Dictionary<int, MapMonsterRow> MapMonsters { get; private set; } = new();
    public Dictionary<int, AiRow> AiConfigs { get; private set; } = new();
    public Dictionary<int, MapConfigRow> MapConfigs { get; private set; } = new();
    public Dictionary<int, PlayerAttrRow> PlayerAttrs { get; private set; } = new();

    // 反向索引: mapName → mapId
    private Dictionary<string, int> _mapNameToId = new();

    public LubanTableLoader(ILogger<LubanTableLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>从候选目录加载所有配置表</summary>
    public void Load()
    {
        string? dataDir = FindDataDir();
        if (dataDir == null)
        {
            _logger.LogWarning("[Tables] data/tables/ not found, no Luban tables loaded");
            return;
        }

        _logger.LogInformation("[Tables] loading from: {Dir}", dataDir);

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        Monsters = LoadTable<MonsterRow>(dataDir, "common_tbmonster.json", opts);
        MapMonsters = LoadTable<MapMonsterRow>(dataDir, "common_tbmapmonster.json", opts);
        AiConfigs = LoadTable<AiRow>(dataDir, "common_tbai.json", opts);
        MapConfigs = LoadTable<MapConfigRow>(dataDir, "common_tbmapconfig.json", opts);
        PlayerAttrs = LoadTable<PlayerAttrRow>(dataDir, "common_tbplayerattr.json", opts);

        // 建立地图名→ID 反向索引
        _mapNameToId = MapConfigs.Values.ToDictionary(m => m.MapName, m => m.Id);

        _logger.LogInformation("[Tables] loaded: {Monsters} monsters, {Spawns} map-spawns, {Ai} AI configs, {Maps} maps",
            Monsters.Count, MapMonsters.Count, AiConfigs.Count, MapConfigs.Count);
    }

    // ---- 查询方法 ----

    /// <summary>获取指定地图的怪物刷新列表</summary>
    public List<MapMonsterRow> GetSpawnsForMap(string mapName)
    {
        if (!_mapNameToId.TryGetValue(mapName, out var mapId)) return new();
        return MapMonsters.Values.Where(s => s.MapId == mapId && s.IsActive).ToList();
    }

    /// <summary>获取 AI 配置</summary>
    public AiRow? GetAi(int aiId) => AiConfigs.GetValueOrDefault(aiId);

    /// <summary>
    /// 获取玩家基础属性（从 TbPlayerAttr 表读取，默认 id=1）
    /// 返回 (hp, mp, agility, patk, matk, pdef, mdef)
    /// </summary>
    public (int hp, int mp, int agility, int patk, int matk, int pdef, int mdef) GetPlayerBaseAttrs(int id = 1)
    {
        var row = PlayerAttrs.GetValueOrDefault(id);
        if (row == null) return (100, 50, 100, 10, 10, 5, 5);
        return (row.Hp, row.Mp, row.Agility, row.Patk, row.Matk, row.Pdef, row.Mdef);
    }

    /// <summary>
    /// 从 Monster 模板的 attrs[] 解析战斗属性。
    /// EMonsterAttr: HP=1, ATK=2, DEF=3
    /// 当 attrs 只有 HP/ATK/DEF 时，ATK 映射为 Patk，DEF 映射为 Pdef，
    /// Matk 默认 = Patk/2，Mdef 默认 = Pdef/2，Agility 默认 100
    /// </summary>
    public (int hp, int maxHp, int patk, int matk, int pdef, int mdef, int agility) ResolveMonsterAttrs(int monsterId)
    {
        var monster = Monsters.GetValueOrDefault(monsterId);
        if (monster == null) return (100, 100, 10, 5, 5, 3, 100);

        var attrs = monster.Attrs;
        if (attrs.Count == 0) return (100, 100, 10, 5, 5, 3, 100);

        var map = attrs.ToDictionary(a => a.AttrKey, a => a.AttrValue);

        int hp = map.GetValueOrDefault(1, 100);       // EMonsterAttr.HP = 1
        int atk = map.GetValueOrDefault(2, 10);        // EMonsterAttr.ATK = 2
        int def = map.GetValueOrDefault(3, 5);          // EMonsterAttr.DEF = 3

        // 当只有基础 3 个属性时，拆分到物攻/魔攻/物防/魔防
        int patk = atk;
        int pdef = def;
        int matk = Math.Max(1, atk / 2);
        int mdef = Math.Max(1, def / 2);
        int agility = 100;

        // 如果将来 xlsx 扩展了更多属性键（使用 EAttr 枚举值），优先使用
        // EAttr: AGILITY=5, PATK=6, MATK=7, PDEF=8, MDEF=9
        if (map.TryGetValue(5, out var agi)) agility = agi;
        if (map.TryGetValue(6, out var pa)) patk = pa;
        if (map.TryGetValue(7, out var ma)) matk = ma;
        if (map.TryGetValue(8, out var pd)) pdef = pd;
        if (map.TryGetValue(9, out var md)) mdef = md;

        return (hp, hp, patk, matk, pdef, mdef, agility);
    }

    // ---- 内部 ----

    private static string[] CandidateDirs = new[]
    {
        "data/tables",
        "../data/tables",
        "../../data/tables",
        "../../../data/tables",
    };

    private static string? FindDataDir()
    {
        foreach (var candidate in CandidateDirs)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full) && Directory.GetFiles(full, "*.json").Length > 0)
                return full;
        }
        return null;
    }

    private Dictionary<int, T> LoadTable<T>(string dir, string fileName, JsonSerializerOptions opts) where T : class
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
        {
            _logger.LogWarning("[Tables] file not found: {File}", fileName);
            return new();
        }
        var json = File.ReadAllText(path);
        var list = JsonSerializer.Deserialize<List<T>>(json, opts) ?? new();
        var idProp = typeof(T).GetProperty("Id");
        if (idProp == null) return list.ToDictionary(_ => 0);

        var dict = new Dictionary<int, T>();
        foreach (var item in list)
        {
            var id = (int)(idProp.GetValue(item) ?? 0);
            dict[id] = item;
        }
        return dict;
    }
}
