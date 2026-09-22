using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 共享技能静态数据 — 从 skill_config.json 加载（客户端只用 6 字段，对齐 Godot SkillDataUtil）。
    /// </summary>
    public static class SkillDataUtil
    {
        public static readonly Dictionary<uint, (string name, int range, double castTime, double cd, int mpCost, int job)> Skills = new()
        {
            // Fallback 数据，Load() 后会被 JSON 覆盖（与 Godot 一致）
            [1] = ("普通攻击", 1, 0.3, 1.5, 0, 0),
            [2] = ("烈斩", 1, 0.8, 3.0, 10, 1),
            [3] = ("盾击", 1, 0.6, 5.0, 15, 1),
            [4] = ("旋风斩", 2, 1.0, 8.0, 15, 1),
            [5] = ("火球术", 3, 1.0, 3.0, 15, 2),
            [6] = ("冰霜新星", 2, 0.8, 5.0, 20, 2),
            [7] = ("奥术飞弹", 3, 0.5, 2.0, 10, 2),
            [9] = ("治疗术", 3, 1.0, 3.0, 15, 3),
            [10] = ("神圣之光", 3, 1.5, 8.0, 30, 3),
            [11] = ("惩击", 2, 0.6, 4.0, 10, 3),
            [12] = ("撕咬", 1, 0.6, 3.0, 0, 4),
            [13] = ("狼嚎", 2, 0.8, 6.0, 0, 4),
            [14] = ("骷髅突刺", 1, 0.5, 2.0, 0, 4),
        };

        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Data", "skill_config.json");
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[SkillDataUtil] skill_config.json not found，使用回退表: " + path);
                    return;
                }

                var rows = JArray.Parse(File.ReadAllText(path));
                if (rows.Count == 0) return;

                // 叠加而非清空（对齐注释意图"被 JSON 覆盖"）：
                // JSON 缺的 id（如 1=普通攻击）保留回退表，JSON 行覆盖同名 id
                foreach (JObject row in rows)
                {
                    uint id = (uint)((int?)row["id"] ?? 0);
                    Skills[id] = (
                        (string)row["name"] ?? "",
                        (int?)row["cast_range"] ?? 0,
                        (double?)row["cast_time"] ?? 0.0,
                        (double?)row["cooldown"] ?? 0.0,
                        (int?)row["mp_cost"] ?? 0,
                        (int?)row["job"] ?? 0);
                }
                Debug.Log($"[SkillDataUtil] Loaded {Skills.Count} skills");
            }
            catch (System.Exception)
            {
                // fallback: keep hardcoded data
            }
        }

        public static (string name, int range, double castTime, double cd, int mpCost, int job) Get(uint id)
        {
            Load();
            return Skills.TryGetValue(id, out var d) ? d : ($"未知技能({id})", 0, 0, 0, 0, 0);
        }

        public static string GetName(uint id)
        {
            Load();
            return Skills.TryGetValue(id, out var d) ? d.name : $"技能{id}";
        }

        /// <summary>返回所有可学习的技能 ID（排除普攻 id≤1 和怪物技能 job=4，对齐 Godot）。</summary>
        public static List<uint> GetAllLearnableIds()
        {
            Load();
            var result = new List<uint>();
            foreach (var kv in Skills)
            {
                if (kv.Key > 1 && kv.Value.job != 4)
                    result.Add(kv.Key);
            }
            result.Sort();
            return result;
        }

        /// <summary>返回指定职业的可学习技能 ID（排除普攻 id≤1 和怪物技能 job=4，对齐 Godot）。</summary>
        public static List<uint> GetLearnableIdsForJob(int jobId)
        {
            Load();
            var result = new List<uint>();
            foreach (var kv in Skills)
            {
                if (kv.Value.job == jobId && kv.Key > 1 && kv.Value.job != 4)
                    result.Add(kv.Key);
            }
            result.Sort();
            return result;
        }

        /// <summary>职业名称转 ID（与 Luban EJobType 枚举一致，对齐 Godot）。</summary>
        public static int JobNameToId(string jobName) => jobName switch
        {
            "战士" => 1,
            "法师" => 2,
            "牧师" => 3,
            _ => 0,
        };
    }
}
