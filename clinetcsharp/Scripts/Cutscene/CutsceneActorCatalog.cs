using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClinetCSharp.Cutscene;

/// <summary>
/// 演出"演员"资源解析（§5.5）：
/// - 玩家演员（type=1）：使用单一预设外观（EntityStyleConfig.CreateNpcDefault → 蓝色系）。
/// - 怪物演员（type=2）：外观与名字绑定 monster_config.json（客户端唯一一张怪物表）。
///   名字取自 MonsterDef.Name；外观当前统一走 EntityStyleConfig.CreateMonsterDefault（红色系）。
///   （注：MonsterDef.UiConfigId → EntityStyleConfig 的精细化外观映射依赖运行时 MonsterManager 单例，
///    编辑器内取不到，故此处用默认怪物样式；monster_config.json 中 UiConfigId 目前也多为默认值 1。）
/// - NPC（type=3）本期不实现。
/// 本类只读 res://data/monster_config.json，不依赖运行时单例，编辑器与运行时通用。
/// </summary>
public static class CutsceneActorCatalog
{
    private const string MonsterTablePath = "res://data/monster_config.json";
    private static MonsterConfigData? Config;

    public static void Load()
    {
        Config = null;
        if (!FileAccess.FileExists(MonsterTablePath)) return;
        using var f = FileAccess.Open(MonsterTablePath, FileAccess.ModeFlags.Read);
        var json = f.GetAsText();
        Config = JsonSerializer.Deserialize<MonsterConfigData>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    /// <summary>编辑器下拉用：所有可选怪物（id, 名字），按 id 升序</summary>
        public static List<(int id, string name)> GetMonsterOptions()
        {
            var list = new List<(int id, string name)>();
        if (Config?.Monsters == null) return list;
        foreach (var m in Config.Monsters)
            list.Add((m.MonsterId, m.Name));
        list.Sort((a, b) => a.id.CompareTo(b.id));
        return list;
    }

    /// <summary>怪物演员的名字（取配置名；找不到回退"怪物{id}"）</summary>
    public static string ResolveMonsterName(int monsterConfigId)
    {
        var def = Config?.Monsters?.FirstOrDefault(m => m.MonsterId == monsterConfigId);
        return def?.Name ?? $"怪物{monsterConfigId}";
    }

    /// <summary>玩家演员外观（单一预设：蓝色系）</summary>
    public static EntityStyleConfig ResolvePlayerStyle()
        => EntityStyleConfig.CreateNpcDefault();

    /// <summary>怪物演员外观（统一默认：红色系）。精细化外观可后续接 UiConfigId→EntityStyleConfig</summary>
    public static EntityStyleConfig ResolveMonsterStyle(int monsterConfigId)
        => EntityStyleConfig.CreateMonsterDefault();

    /// <summary>兜底：用单一颜色构造最小样式（StoryActor 兼容重载会调用，1 参数）</summary>
    public static EntityStyleConfig BuildStyleFromColor(Color c)
    {
        var style = new EntityStyleConfig
        {
            BorderColor = c,
            BgColor = c.Darkened(0.15f),
            TextColor = new Color(1, 1, 1),
        };
        return style;
    }
}
