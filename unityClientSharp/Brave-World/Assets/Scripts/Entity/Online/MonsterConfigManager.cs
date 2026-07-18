using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 怪物配置（静态版）。
    /// 移植自 clinetcsharp/Scripts/MonsterConfigManager.cs：显示层只消费 UiConfigId 与 Quality，
    /// 其余战斗数值（patk 等）按 Godot 现状忽略。
    /// </summary>
    public static class MonsterConfigManager
    {
        public class MonsterDef
        {
            public int MonsterId;
            public string Name = "";
            public string Quality = "普通";
            public int UiConfigId = 1;
        }

        private static readonly Dictionary<int, MonsterDef> _defs = new();
        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Data", "monster_config.json");
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[MonsterConfigManager] monster_config.json not found: " + path);
                    return;
                }

                var root = JObject.Parse(File.ReadAllText(path));
                foreach (var row in root["monsters"] as JArray ?? new JArray())
                {
                    int id = (int?)row["monsterId"] ?? 0;
                    _defs[id] = new MonsterDef
                    {
                        MonsterId = id,
                        Name = (string)row["name"] ?? "",
                        Quality = (string)row["quality"] ?? "普通",
                        UiConfigId = (int?)row["uiConfigId"] ?? 1,
                    };
                }
                Debug.Log($"[MonsterConfigManager] Loaded {_defs.Count} monster defs");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MonsterConfigManager] Failed to load: {ex.Message}");
            }
        }

        public static MonsterDef Get(int monsterId)
        {
            Load();
            return _defs.TryGetValue(monsterId, out var def) ? def : null;
        }

        /// <summary>品质名（缺省"普通"，对齐 Godot）。</summary>
        public static string GetQuality(int monsterId) => Get(monsterId)?.Quality ?? "普通";
    }
}
