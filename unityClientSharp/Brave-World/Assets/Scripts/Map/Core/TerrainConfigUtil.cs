using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityClientSharp.Map.Core
{
    /// <summary>
    /// 地形配置静态数据 — 从 terrain_config.json 加载。
    /// 移植自 clinetcsharp/Scripts/TerrainConfigUtil.cs：Godot.FileAccess -> System.IO.File；
    /// JSON 解析使用 Newtonsoft.Json（随仓库 vendoring 于 Assets/Plugins/NewtonsoftJson，本引擎不含 System.Text.Json）。
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
                string path = Path.Combine(Application.streamingAssetsPath, "Data", "terrain_config.json");
                if (!File.Exists(path))
                {
                    Debug.LogError("[TerrainConfigUtil] terrain_config.json not found: " + path);
                    return;
                }

                string json = File.ReadAllText(path);
                var arr = JArray.Parse(json);
                foreach (JObject row in arr)
                {
                    int id = (int?)row["id"] ?? 0;
                    Configs[id] = new TerrainConfig
                    {
                        Id = id,
                        Name = (string)row["name"] ?? "",
                        Description = (string)row["description"] ?? "",
                        Walkable = (bool?)row["walkable"] ?? true,
                        MoveSpeedRatio = (float?)(double?)row["move_speed_ratio"] ?? 1.0f,
                        CanSwim = (bool?)row["can_swim"] ?? false,
                        PatkModifier = (float?)(double?)row["patk_modifier"] ?? 1.0f,
                        MatkModifier = (float?)(double?)row["matk_modifier"] ?? 1.0f,
                        PdefModifier = (float?)(double?)row["pdef_modifier"] ?? 1.0f,
                        MdefModifier = (float?)(double?)row["mdef_modifier"] ?? 1.0f,
                        HpRegenPerSec = (int?)row["hp_regen_per_sec"] ?? 0,
                        MpRegenPerSec = (int?)row["mp_regen_per_sec"] ?? 0,
                        FireDamageBonus = (int?)row["fire_damage_bonus"] ?? 0,
                        IceDamageBonus = (int?)row["ice_damage_bonus"] ?? 0,
                        PoisonDamageBonus = (int?)row["poison_damage_bonus"] ?? 0,
                        ColorR = (int?)row["color_r"] ?? 200,
                        ColorG = (int?)row["color_g"] ?? 200,
                        ColorB = (int?)row["color_b"] ?? 200,
                        ColorA = (float?)(double?)row["color_a"] ?? 0.0f,
                        ParticleEffect = (string)row["particle_effect"] ?? "",
                    };
                }

                Debug.Log($"[TerrainConfigUtil] Loaded {Configs.Count} terrain configs");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TerrainConfigUtil] Failed to load: {ex.Message}");
            }
        }

        public static TerrainConfig Get(int id)
        {
            return Configs.TryGetValue(id, out var c) ? c : (Configs.TryGetValue(0, out var def) ? def : null);
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
}
