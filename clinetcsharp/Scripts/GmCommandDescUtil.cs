using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// GM 命令说明静态数据 — 从 Luban 导出的 JSON 加载（客户端专用）
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
                var file = FileAccess.Open("res://data/gm_command_desc.json", FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr("[GmCommandDescUtil] gm_command_desc.json not found");
                    return;
                }

                string json = file.GetAsText();
                file.Close();

                var rows = JsonSerializer.Deserialize<List<GmCommandDescJsonRow>>(json);
                if (rows == null || rows.Count == 0) return;

                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.name))
                        continue;
                    Descriptions[row.name] = row.description ?? "";
                    ParamHints[row.name] = row.param_hint ?? "";
                }

                GD.Print($"[GmCommandDescUtil] Loaded {Descriptions.Count} GM command descriptions from Luban table");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[GmCommandDescUtil] Failed to load: {ex.Message}");
            }
        }

        public static string GetDescription(string commandName)
        {
            return Descriptions.TryGetValue(commandName, out var desc) ? desc : "";
        }

        public static string GetParamHint(string commandName)
        {
            return ParamHints.TryGetValue(commandName, out var hint) ? hint : "";
        }

        private class GmCommandDescJsonRow
        {
            public string name { get; set; } = "";
            public string description { get; set; } = "";
            public string param_hint { get; set; } = "";
        }
    }
}
