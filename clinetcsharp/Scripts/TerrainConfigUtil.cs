using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 地形配置静态数据 — 从 Luban 导出的 JSON 加载
    /// </summary>
    public static class TerrainConfigUtil
    {
        public static readonly Dictionary<int, TerrainConfig> Configs = new();

        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                var file = FileAccess.Open("res://data/terrain_config.json", FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr("[TerrainConfigUtil] terrain_config.json not found");
                    return;
                }

                string json = file.GetAsText();
                file.Close();

                var rows = JsonSerializer.Deserialize<List<TerrainJsonRow>>(json);
                if (rows == null || rows.Count == 0) return;

                foreach (var row in rows)
                {
                    Configs[row.id] = new TerrainConfig
                    {
                        Id = row.id,
                        Name = row.name,
                        Description = row.description,
                        Walkable = row.walkable,
                        MoveSpeedRatio = row.move_speed_ratio,
                        CanSwim = row.can_swim,
                        PatkModifier = row.patk_modifier,
                        MatkModifier = row.matk_modifier,
                        PdefModifier = row.pdef_modifier,
                        MdefModifier = row.mdef_modifier,
                        HpRegenPerSec = row.hp_regen_per_sec,
                        MpRegenPerSec = row.mp_regen_per_sec,
                        FireDamageBonus = row.fire_damage_bonus,
                        IceDamageBonus = row.ice_damage_bonus,
                        PoisonDamageBonus = row.poison_damage_bonus,
                        ColorR = row.color_r,
                        ColorG = row.color_g,
                        ColorB = row.color_b,
                        ColorA = row.color_a,
                        ParticleEffect = row.particle_effect,
                    };
                }

                GD.Print($"[TerrainConfigUtil] Loaded {Configs.Count} terrain configs from Luban table");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[TerrainConfigUtil] Failed to load: {ex.Message}");
            }
        }

        public static TerrainConfig Get(int id)
        {
            return Configs.TryGetValue(id, out var c) ? c : Configs.GetValueOrDefault(0);
        }

        public static string GetName(int id)
        {
            return Configs.TryGetValue(id, out var c) ? c.Name : "未知地形";
        }
    }

    public class TerrainConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Walkable { get; set; } = true;
        public float MoveSpeedRatio { get; set; } = 1.0f;
        public bool CanSwim { get; set; } = false;
        public float PatkModifier { get; set; } = 1.0f;
        public float MatkModifier { get; set; } = 1.0f;
        public float PdefModifier { get; set; } = 1.0f;
        public float MdefModifier { get; set; } = 1.0f;
        public int HpRegenPerSec { get; set; } = 0;
        public int MpRegenPerSec { get; set; } = 0;
        public int FireDamageBonus { get; set; } = 0;
        public int IceDamageBonus { get; set; } = 0;
        public int PoisonDamageBonus { get; set; } = 0;
        public int ColorR { get; set; } = 200;
        public int ColorG { get; set; } = 200;
        public int ColorB { get; set; } = 200;
        public float ColorA { get; set; } = 0.0f;
        public string ParticleEffect { get; set; } = "";
    }

    internal class TerrainJsonRow
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string description { get; set; } = "";
        public bool walkable { get; set; }
        public float move_speed_ratio { get; set; }
        public bool can_swim { get; set; }
        public float patk_modifier { get; set; }
        public float matk_modifier { get; set; }
        public float pdef_modifier { get; set; }
        public float mdef_modifier { get; set; }
        public int hp_regen_per_sec { get; set; }
        public int mp_regen_per_sec { get; set; }
        public int fire_damage_bonus { get; set; }
        public int ice_damage_bonus { get; set; }
        public int poison_damage_bonus { get; set; }
        public int color_r { get; set; }
        public int color_g { get; set; }
        public int color_b { get; set; }
        public float color_a { get; set; }
        public string particle_effect { get; set; } = "";
    }
}
