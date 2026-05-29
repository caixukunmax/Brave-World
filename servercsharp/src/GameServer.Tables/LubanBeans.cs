using System.Text.Json.Serialization;

namespace GameServer.Tables;

public enum ERespawnType
{
    SpawnPoint = 0,
    DeathPoint = 1,
    RandomNearSpawn = 2,
}

public enum ESkillTargetType
{
    SingleEnemy = 1,
    AllEnemiesInRange = 2,
    Self = 3,
    AllAlliesInRange = 4,
}

public enum CombatBehaviorType
{
    Auto = 0,
    Melee = 1,
    Ranged = 2,
    Caster = 3,
}

public enum EBuffType
{
    Buff = 1,
    Debuff = 2,
}

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
    [JsonPropertyName("drop_group_id")] public int DropGroupId { get; set; }
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
    [JsonPropertyName("respawn_type")] public ERespawnType RespawnType { get; set; }
    [JsonPropertyName("respawn_range")] public int RespawnRange { get; set; }
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
    [JsonPropertyName("param_1")] public CombatBehaviorType Param1 { get; set; }
    [JsonPropertyName("param_2")] public int Param2 { get; set; }
    [JsonPropertyName("param_3")] public double Param3 { get; set; }
    [JsonPropertyName("territory_radius")] public int? TerritoryRadius { get; set; }
    [JsonPropertyName("chase_timeout")] public double? ChaseTimeout { get; set; }
    [JsonPropertyName("return_buff_id")] public int? ReturnBuffId { get; set; }
    [JsonPropertyName("return_speed_multiplier")] public double? ReturnSpeedMultiplier { get; set; }
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
    [JsonPropertyName("patk")] public int Patk { get; set; }
    [JsonPropertyName("matk")] public int Matk { get; set; }
    [JsonPropertyName("pdef")] public int Pdef { get; set; }
    [JsonPropertyName("mdef")] public int Mdef { get; set; }
    [JsonPropertyName("mp_regen")] public int MpRegen { get; set; }
    [JsonPropertyName("first_strike_haste")] public int FirstStrikeHaste { get; set; }
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
    [JsonPropertyName("buff_id")] public int BuffId { get; set; }
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
    [JsonPropertyName("target_type")] public ESkillTargetType TargetType { get; set; } = ESkillTargetType.SingleEnemy;
    [JsonPropertyName("actions")] public List<CombatActionBeanRow> Actions { get; set; } = new();
    [JsonPropertyName("job")] public int Job { get; set; }
    [JsonPropertyName("projectile_speed")] public float ProjectileSpeed { get; set; }
    [JsonPropertyName("projectile_max_range")] public int ProjectileMaxRange { get; set; }
}

// ---- Job ----

public class JobRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("default_learned_skills")] public List<int> DefaultLearnedSkills { get; set; } = new();
    [JsonPropertyName("default_equipped_skills")] public List<int> DefaultEquippedSkills { get; set; } = new();
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

// ---- Buff ----

public class BuffEffectRow
{
    [JsonPropertyName("trigger")] public string Trigger { get; set; } = "";
    [JsonPropertyName("action_type")] public int ActionType { get; set; }
    [JsonPropertyName("damage_type")] public int DamageType { get; set; }
    [JsonPropertyName("coefficient")] public double Coefficient { get; set; }
    [JsonPropertyName("buff_id")] public int BuffId { get; set; }
}

public class BuffAttrModifierRow
{
    [JsonPropertyName("attr")] public string Attr { get; set; } = "";
    [JsonPropertyName("value")] public double Value { get; set; }
    [JsonPropertyName("is_pct")] public bool IsPct { get; set; }
}

public class BuffConfigRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("buff_type")] public EBuffType BuffType { get; set; } = EBuffType.Buff;
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
    [JsonPropertyName("duration")] public double Duration { get; set; }
    [JsonPropertyName("max_stacks")] public int MaxStacks { get; set; } = 1;
    [JsonPropertyName("stack_rule")] public string StackRule { get; set; } = "Refresh";
    [JsonPropertyName("tick_interval")] public double TickInterval { get; set; }
    [JsonPropertyName("effects")] public List<BuffEffectRow> Effects { get; set; } = new();
    [JsonPropertyName("attr_modifiers")] public List<BuffAttrModifierRow> AttrModifiers { get; set; } = new();
    [JsonPropertyName("shield_base")] public int ShieldBase { get; set; }
    [JsonPropertyName("clear_on_disengage")] public bool ClearOnDisengage { get; set; } = true;
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
}

// ---- DropGroup ----

public class DropEntryRow
{
    [JsonPropertyName("item_id")] public int ItemId { get; set; }
    [JsonPropertyName("count_min")] public int CountMin { get; set; }
    [JsonPropertyName("count_max")] public int CountMax { get; set; }
    [JsonPropertyName("weight")] public int Weight { get; set; }
    [JsonPropertyName("guaranteed")] public bool Guaranteed { get; set; }
}

public class DropGroupRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("entries")] public List<DropEntryRow> Entries { get; set; } = new();
}

// ---- Item ----

public class ItemRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("major_type")] public int MajorType { get; set; }
    [JsonPropertyName("minor_type")] public int MinorType { get; set; }
    [JsonPropertyName("max_pile_num")] public int MaxPileNum { get; set; }
    [JsonPropertyName("quality")] public int Quality { get; set; }
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
    [JsonPropertyName("icon_backgroud")] public string IconBackground { get; set; } = "";
    [JsonPropertyName("icon_mask")] public string IconMask { get; set; } = "";
    [JsonPropertyName("desc")] public string Desc { get; set; } = "";
    [JsonPropertyName("show_order")] public int ShowOrder { get; set; }
    [JsonPropertyName("effect_type")] public string EffectType { get; set; } = "";
    [JsonPropertyName("effect_value")] public int EffectValue { get; set; }
    [JsonPropertyName("price")] public int Price { get; set; }
    [JsonPropertyName("can_sell")] public bool CanSell { get; set; }
    [JsonPropertyName("obtain_methods")] public string ObtainMethods { get; set; } = "";
    [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = "";
}

// ---- Terrain ----

public class TerrainConfigRow
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("walkable")] public bool Walkable { get; set; }
    [JsonPropertyName("move_speed_ratio")] public float MoveSpeedRatio { get; set; }
    [JsonPropertyName("can_swim")] public bool CanSwim { get; set; }
    [JsonPropertyName("patk_modifier")] public float PatkModifier { get; set; }
    [JsonPropertyName("matk_modifier")] public float MatkModifier { get; set; }
    [JsonPropertyName("pdef_modifier")] public float PdefModifier { get; set; }
    [JsonPropertyName("mdef_modifier")] public float MdefModifier { get; set; }
    [JsonPropertyName("hp_regen_per_sec")] public int HpRegenPerSec { get; set; }
    [JsonPropertyName("mp_regen_per_sec")] public int MpRegenPerSec { get; set; }
    [JsonPropertyName("fire_damage_bonus")] public int FireDamageBonus { get; set; }
    [JsonPropertyName("ice_damage_bonus")] public int IceDamageBonus { get; set; }
    [JsonPropertyName("poison_damage_bonus")] public int PoisonDamageBonus { get; set; }
    [JsonPropertyName("color_r")] public int ColorR { get; set; }
    [JsonPropertyName("color_g")] public int ColorG { get; set; }
    [JsonPropertyName("color_b")] public int ColorB { get; set; }
    [JsonPropertyName("color_a")] public float ColorA { get; set; }
    [JsonPropertyName("particle_effect")] public string ParticleEffect { get; set; } = "";
}
