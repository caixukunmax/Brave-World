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
    public Dictionary<int, CombatLogTextRow> CombatLogTexts { get; private set; } = new();
    public Dictionary<int, SkillConfigRow> Skills { get; private set; } = new();
    public Dictionary<int, BuffConfigRow> Buffs { get; private set; } = new();
    public Dictionary<int, JobRow> Jobs { get; private set; } = new();
    public Dictionary<int, CombatNarrationRow> CombatNarrations { get; private set; } = new();
    public Dictionary<int, LevelUpRow> LevelUps { get; private set; } = new();
    public Dictionary<int, DropGroupRow> DropGroups { get; private set; } = new();
    public Dictionary<int, TerrainConfigRow> TerrainConfigs { get; private set; } = new();
    public Dictionary<int, ItemRow> Items { get; private set; } = new();

    // 反向索引: mapName → mapId
    private Dictionary<string, int> _mapNameToId = new();

    public LubanTableLoader(ILogger<LubanTableLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>从候选目录加载所有配置表</summary>
    public void Load()
    {
        string? dataDir = RuntimeDataPathResolver.FindTablesDir();
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
        CombatLogTexts = LoadTable<CombatLogTextRow>(dataDir, "common_tbcombatlogtext.json", opts);
        Skills = LoadTable<SkillConfigRow>(dataDir, "common_tbskill.json", opts);
        Buffs = LoadTable<BuffConfigRow>(dataDir, "common_tbbuff.json", opts);
        Jobs = LoadTable<JobRow>(dataDir, "common_tbjob.json", opts);
        CombatNarrations = LoadTable<CombatNarrationRow>(dataDir, "common_tbcombatnarration.json", opts);
        LevelUps = LoadTable<LevelUpRow>(dataDir, "common_tblevelup.json", opts);
        DropGroups = LoadTable<DropGroupRow>(dataDir, "common_tbdropgroup.json", opts);
        Items = LoadTable<ItemRow>(dataDir, "item_tbitem.json", opts);
        TerrainConfigs = LoadTable<TerrainConfigRow>(dataDir, "common_tbterrainconfig.json", opts);

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
    public SkillConfigRow? GetSkill(int id) => Skills.GetValueOrDefault(id);

    /// <summary>获取 Buff 配置</summary>
    public BuffConfigRow? GetBuff(int id) => Buffs.GetValueOrDefault(id);
    public JobRow? GetJob(int id) => Jobs.GetValueOrDefault(id);
    public JobRow? GetJobByName(string name) => Jobs.Values.FirstOrDefault(j => j.Name == name);

    /// <summary>
    /// 获取指定职业的默认技能配置
    /// </summary>
    public (List<int> learned, List<int> equipped) GetJobDefaultSkills(string jobName)
    {
        var job = GetJobByName(jobName);
        if (job == null) return (new List<int>(), new List<int>());
        return (job.DefaultLearnedSkills, job.DefaultEquippedSkills);
    }

    /// <summary>获取指定职业的所有技能ID</summary>
    public List<int> GetSkillsForJob(int jobId) => Skills.Values.Where(s => s.Job == jobId).Select(s => s.Id).ToList();

    /// <summary>获取怪物的技能池（从怪物配置表读取）</summary>
    public List<int> GetMonsterSkills(int monsterId)
    {
        var monster = Monsters.GetValueOrDefault(monsterId);
        return monster?.Skills ?? new List<int>();
    }

    /// <summary>获取指定条件的叙事触发器列表</summary>
    public List<CombatNarrationRow> GetNarrationsByCondition(string condition)
    {
        return CombatNarrations.Values.Where(n => n.Condition == condition).ToList();
    }

    /// <summary>获取指定等级的升级配置，找不到返回 null（已满级）</summary>
    public LevelUpRow? GetLevelUp(int level) => LevelUps.GetValueOrDefault(level);

    /// <summary>获取掉落组配置</summary>
    public DropGroupRow? GetDropGroup(int id) => DropGroups.GetValueOrDefault(id);

    /// <summary>获取道具配置</summary>
    public ItemRow? GetItem(int id) => Items.GetValueOrDefault(id);

    /// <summary>获取地形配置</summary>
    public TerrainConfigRow? GetTerrainConfig(int id) => TerrainConfigs.GetValueOrDefault(id);

    /// <summary>获取最大等级</summary>
    public int GetMaxLevel() => LevelUps.Count > 0 ? LevelUps.Keys.Max() : 1;

    /// <summary>获取怪物的经验奖励</summary>
    public int GetMonsterExp(int monsterId)
    {
        var monster = Monsters.GetValueOrDefault(monsterId);
        return monster?.Exp ?? 0;
    }

    private static List<int> ParseIntList(string s)
    {
        if (string.IsNullOrEmpty(s)) return new List<int>();
        return s.Split(',').Where(p => !string.IsNullOrWhiteSpace(p)).Select(int.Parse).ToList();
    }

    /// <summary>
    /// 获取玩家基础属性（从 TbPlayerAttr 表读取，默认 id=1）
    /// 返回 (hp, mp, patk, matk, pdef, mdef, mpRegen)
    /// </summary>
    public (int hp, int mp, int patk, int matk, int pdef, int mdef, int mpRegen) GetPlayerBaseAttrs(int id = 1)
    {
        var row = PlayerAttrs.GetValueOrDefault(id);
        if (row == null) return (100, 50, 10, 10, 5, 5, 2);
        return (row.Hp, row.Mp, row.Patk, row.Matk, row.Pdef, row.Mdef, row.MpRegen);
    }

    /// <summary>
    /// 获取玩家先攻加速值（从 TbPlayerAttr 表读取，默认 id=1）
    /// </summary>
    public int GetPlayerFirstStrikeHaste(int id = 1)
    {
        var row = PlayerAttrs.GetValueOrDefault(id);
        return row?.FirstStrikeHaste ?? 30;
    }

    /// <summary>
    /// 获取指定等级的玩家完整属性（基础 + 等级成长）
    /// 返回 (hp, mp, patk, matk, pdef, mdef, mpRegen)
    /// </summary>
    public (int hp, int mp, int patk, int matk, int pdef, int mdef, int mpRegen) GetPlayerAttrsByLevel(int level)
    {
        var (hp, mp, patk, matk, pdef, mdef, mpRegen) = GetPlayerBaseAttrs();
        for (int lv = 2; lv <= level; lv++)
        {
            var cfg = GetLevelUp(lv);
            if (cfg == null) continue;
            hp += cfg.Hp;
            mp += cfg.Mp;
            patk += cfg.Patk;
            matk += cfg.Matk;
            pdef += cfg.Pdef;
            mdef += cfg.Mdef;
        }
        return (hp, mp, patk, matk, pdef, mdef, mpRegen);
    }

    /// <summary>
    /// 从 Monster 模板的 attrs[] 解析战斗属性。
    /// EMonsterAttr: HP=1, ATK=2, DEF=3
    /// 当 attrs 只有 HP/ATK/DEF 时，ATK 映射为 Patk，DEF 映射为 Pdef，
    /// Matk 默认 = Patk/2，Mdef 默认 = Pdef/2
    /// </summary>
    public (int hp, int maxHp, int patk, int matk, int pdef, int mdef) ResolveMonsterAttrs(int monsterId)
    {
        var monster = Monsters.GetValueOrDefault(monsterId);
        if (monster == null) return (100, 100, 10, 5, 5, 3);

        var attrs = monster.Attrs;
        if (attrs.Count == 0) return (100, 100, 10, 5, 5, 3);

        var map = attrs.ToDictionary(a => a.AttrKey, a => a.AttrValue);

        int hp = map.GetValueOrDefault(1, 100);       // EMonsterAttr.HP = 1
        int atk = map.GetValueOrDefault(2, 10);        // EMonsterAttr.ATK = 2
        int def = map.GetValueOrDefault(3, 5);          // EMonsterAttr.DEF = 3

        // 当只有基础 3 个属性时，拆分到物攻/魔攻/物防/魔防
        int patk = atk;
        int pdef = def;
        int matk = Math.Max(1, atk / 2);
        int mdef = Math.Max(1, def / 2);

        // 配置表 attrs 格式: HP=1|ATK=2|DEF=3|AGILITY=5
        // attr_key=5 是 AGILITY（敏捷/闪避相关），不是 PATK
        // 若将来需扩展 PATK/MATK/PDEF/MDEF，应在配置表中新增对应字段
        if (map.TryGetValue(6, out var ma)) matk = ma;
        if (map.TryGetValue(7, out var pd)) pdef = pd;
        if (map.TryGetValue(8, out var md)) mdef = md;

        return (hp, hp, patk, matk, pdef, mdef);
    }

    // ---- 内部 ----

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
