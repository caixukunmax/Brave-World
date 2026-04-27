using System.Text.Json.Serialization;

namespace GameServer.Tables;

// ---- Monster ----

public class MonsterRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("exp")] public int Exp { get; set; }
    [JsonPropertyName("drop_items")] public string DropItems { get; set; } = "";
    [JsonPropertyName("attrs")] public List<MonsterAttrRow> Attrs { get; set; } = new();
    [JsonPropertyName("skills")] public List<int> Skills { get; set; } = new();
}

public class MonsterAttrRow
{
    [JsonPropertyName("attr_key")] public int AttrKey { get; set; }
    [JsonPropertyName("attr_value")] public int AttrValue { get; set; }
}

// ---- MapMonster ----

public class MapMonsterRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("map_id")] public int MapId { get; set; }
    [JsonPropertyName("monster_id")] public int MonsterId { get; set; }
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("respawn_time")] public int RespawnTime { get; set; }
    [JsonPropertyName("is_active")] public bool IsActive { get; set; } = true;
    [JsonPropertyName("ai_id")] public int AiId { get; set; }
}

// ---- Ai ----

public class AiRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("ai_type")] public string AiType { get; set; } = "patrol";
    [JsonPropertyName("aggro_range")] public int AggroRange { get; set; }
    [JsonPropertyName("move_interval_ms")] public int MoveIntervalMs { get; set; }
    [JsonPropertyName("chase_interval_ms")] public int ChaseIntervalMs { get; set; }
    [JsonPropertyName("max_chase_distance")] public int MaxChaseDistance { get; set; }
    [JsonPropertyName("patrol_range")] public int PatrolRange { get; set; }
    [JsonPropertyName("param_1")] public int Param1 { get; set; }
    [JsonPropertyName("param_2")] public int Param2 { get; set; }
    [JsonPropertyName("param_3")] public double Param3 { get; set; }
}

// ---- MapConfig ----

public class MapConfigRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("map_name")] public string MapName { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("spawn_x")] public int SpawnX { get; set; }
    [JsonPropertyName("spawn_y")] public int SpawnY { get; set; }
}

// ---- PlayerAttr ----

public class PlayerAttrRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("hp")] public int Hp { get; set; }
    [JsonPropertyName("mp")] public int Mp { get; set; }
    [JsonPropertyName("agility")] public int Agility { get; set; }
    [JsonPropertyName("patk")] public int Patk { get; set; }
    [JsonPropertyName("matk")] public int Matk { get; set; }
    [JsonPropertyName("pdef")] public int Pdef { get; set; }
    [JsonPropertyName("mdef")] public int Mdef { get; set; }
    [JsonPropertyName("mp_regen")] public int MpRegen { get; set; }
}

// ---- CombatLogText ----

public class CombatLogTextRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("template")] public string Template { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "";
}

// ---- Skill ----

public class CombatActionBeanRow
{
    [JsonPropertyName("action_type")] public int ActionType { get; set; }
    [JsonPropertyName("damage_type")] public int DamageType { get; set; }
    [JsonPropertyName("coefficient")] public double Coefficient { get; set; }
}

public class SkillConfigRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("cast_range")] public int CastRange { get; set; }
    [JsonPropertyName("cast_time")] public double CastTime { get; set; }
    [JsonPropertyName("interrupt_on_move")] public bool InterruptOnMove { get; set; }
    [JsonPropertyName("post_cast_time")] public double PostCastTime { get; set; }
    [JsonPropertyName("cooldown")] public double Cooldown { get; set; }
    [JsonPropertyName("mp_cost")] public int MpCost { get; set; }
    [JsonPropertyName("target_type")] public string TargetType { get; set; } = "SingleEnemy";
    [JsonPropertyName("actions")] public List<CombatActionBeanRow> Actions { get; set; } = new();
    [JsonPropertyName("job")] public int Job { get; set; }
}

// ---- Job ----

public class JobRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("default_learned_skills")] public string DefaultLearnedSkills { get; set; } = "";
    [JsonPropertyName("default_equipped_skills")] public string DefaultEquippedSkills { get; set; } = "";
    [JsonPropertyName("base_hp")] public int BaseHp { get; set; }
    [JsonPropertyName("base_mp")] public int BaseMp { get; set; }
    [JsonPropertyName("base_atk")] public int BaseAtk { get; set; }
    [JsonPropertyName("base_def")] public int BaseDef { get; set; }
}

// ---- CombatNarration ----

public class CombatNarrationRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("condition")] public string Condition { get; set; } = "";
    [JsonPropertyName("threshold")] public double Threshold { get; set; }
    [JsonPropertyName("probability")] public double Probability { get; set; }
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("cooldown")] public double Cooldown { get; set; }
}

// ---- LevelUp ----

public class LevelUpRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("exp_required")] public int ExpRequired { get; set; }
    [JsonPropertyName("hp")] public int Hp { get; set; }
    [JsonPropertyName("mp")] public int Mp { get; set; }
    [JsonPropertyName("patk")] public int Patk { get; set; }
    [JsonPropertyName("matk")] public int Matk { get; set; }
    [JsonPropertyName("pdef")] public int Pdef { get; set; }
    [JsonPropertyName("mdef")] public int Mdef { get; set; }
    [JsonPropertyName("agility")] public int Agility { get; set; }
}
