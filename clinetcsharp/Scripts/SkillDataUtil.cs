using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 共享技能静态数据 — 从 Luban 导出的 JSON 加载
    /// </summary>
    public static class SkillDataUtil
    {
        public static readonly Dictionary<uint, (string name, int range, double castTime, double cd, int mpCost, int job)> Skills = new()
        {
            // Fallback 数据，Load() 后会被 JSON 覆盖
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
                var file = FileAccess.Open("res://data/skill_config.json", FileAccess.ModeFlags.Read);
                if (file == null) return;

                string json = file.GetAsText();
                file.Close();

                var rows = JsonSerializer.Deserialize<List<SkillJsonRow>>(json);
                if (rows == null || rows.Count == 0) return;

                Skills.Clear();
                foreach (var row in rows)
                {
                    Skills[(uint)row.id] = (row.name, row.cast_range, row.cast_time, row.cooldown, row.mp_cost, row.job);
                }
            }
            catch
            {
                // fallback: keep hardcoded data
            }
        }

        public static (string name, int range, double castTime, double cd, int mpCost, int job) Get(uint id)
        {
            return Skills.TryGetValue(id, out var d) ? d : ($"未知技能({id})", 0, 0, 0, 0, 0);
        }

        public static string GetName(uint id)
        {
            return Skills.TryGetValue(id, out var d) ? d.name : $"技能{id}";
        }

        /// <summary>返回所有可学习的技能 ID（排除怪物技能 job=4）</summary>
        public static List<uint> GetAllLearnableIds()
        {
            var result = new List<uint>();
            foreach (var kv in Skills)
            {
                if (kv.Key > 1 && kv.Value.job != 4)
                    result.Add(kv.Key);
            }
            result.Sort();
            return result;
        }

        /// <summary>返回指定职业的可学习技能 ID（排除怪物技能 job=4）</summary>
        public static List<uint> GetLearnableIdsForJob(int jobId)
        {
            var result = new List<uint>();
            foreach (var kv in Skills)
            {
                if (kv.Value.job == jobId && kv.Key > 1 && kv.Value.job != 4)
                    result.Add(kv.Key);
            }
            result.Sort();
            return result;
        }

        /// <summary>职业名称转 ID（与 Luban EJobType 枚举一致）</summary>
        public static int JobNameToId(string jobName) => jobName switch
        {
            "战士" => 1,
            "法师" => 2,
            "牧师" => 3,
            _ => 0,
        };

        private class SkillJsonRow
        {
            public int id { get; set; }
            public string name { get; set; } = "";
            public int cast_range { get; set; }
            public double cast_time { get; set; }
            public double cooldown { get; set; }
            public int mp_cost { get; set; }
            public int job { get; set; }
        }
    }
}
