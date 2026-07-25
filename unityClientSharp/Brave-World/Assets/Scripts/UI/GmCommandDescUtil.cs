using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// GM 命令说明静态数据 — 移植自 clinetcsharp/Scripts/GmCommandDescUtil.cs。
    /// 从 Luban 导出的 JSON（StreamingAssets/Data/gm_command_desc.json，与 Godot res://data 同一份）加载：
    /// 命令名 → 描述 / 参数提示（param_hint 按逗号分隔多个参数名）。
    /// </summary>
    public static class GmCommandDescUtil
    {
        public static readonly Dictionary<string, string> Descriptions = new();
        public static readonly Dictionary<string, string> ParamHints = new();

        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Data", "gm_command_desc.json");
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[GmCommandDescUtil] gm_command_desc.json not found: " + path);
                    return;
                }

                foreach (JObject row in JArray.Parse(File.ReadAllText(path)))
                {
                    string name = (string)row["name"] ?? "";
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    Descriptions[name] = (string)row["description"] ?? "";
                    ParamHints[name] = (string)row["param_hint"] ?? "";
                }

                Debug.Log($"[GmCommandDescUtil] Loaded {Descriptions.Count} GM command descriptions");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GmCommandDescUtil] Failed to load: {ex.Message}");
            }
        }

        public static string GetDescription(string commandName)
        {
            Load();
            return commandName != null && Descriptions.TryGetValue(commandName, out var desc) ? desc : "";
        }

        public static string GetParamHint(string commandName)
        {
            Load();
            return commandName != null && ParamHints.TryGetValue(commandName, out var hint) ? hint : "";
        }
    }
}
