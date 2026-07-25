using System.Collections.Generic;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// GM 命令自动补全匹配逻辑（纯函数，无引擎依赖，便于 EditMode 测试）。
    /// 逐行移植自 clinetcsharp/Scripts/GMPanel.Actions.cs 的建议匹配部分，
    /// 规则见 docs/design/gm-panel-autocomplete.md 第 4 节：
    /// - 输入无逗号：按命令名聚合去重，前缀匹配优先于包含匹配，同级按长度升序；
    /// - 输入含逗号且逗号前是完整命令名：对完整命令串匹配（参数变体）；
    /// - 输入含逗号但逗号前不是完整命令名：退回命令名聚合模式；
    /// - 最多 8 条；有参数的命令名候选填入时自动补逗号。
    /// </summary>
    public static class GmCommandSuggester
    {
        public const int MaxSuggestions = 8;

        /// <summary>一条已配置命令（面板 GmCommand 的纯数据投影）。</summary>
        public readonly struct CommandEntry
        {
            public CommandEntry(string label, string cmd, string description)
            {
                Label = label;
                Cmd = cmd;
                Description = description;
            }

            public string Label { get; }
            public string Cmd { get; }
            public string Description { get; }
        }

        public class Suggestion
        {
            public string DisplayText;
            public string InsertText;
        }

        public static string GetCommandName(string cmd)
        {
            int commaIndex = cmd.IndexOf(',');
            return commaIndex >= 0 ? cmd.Substring(0, commaIndex) : cmd;
        }

        public static bool IsExistingCommandName(string name, IReadOnlyList<CommandEntry> commands)
        {
            string lowerName = name.ToLower();
            foreach (var command in commands)
            {
                if (GetCommandName(command.Cmd).ToLower() == lowerName)
                    return true;
            }
            return false;
        }

        public static List<Suggestion> CollectSuggestions(string input, IReadOnlyList<CommandEntry> commands, int maxSuggestions = MaxSuggestions)
        {
            if (input.Contains(","))
            {
                string namePart = GetCommandName(input);
                if (IsExistingCommandName(namePart, commands))
                    return CollectFullCommandSuggestions(input, commands, maxSuggestions);

                return CollectNameSuggestions(namePart, commands, maxSuggestions);
            }

            return CollectNameSuggestions(input, commands, maxSuggestions);
        }

        private static List<Suggestion> CollectFullCommandSuggestions(string input, IReadOnlyList<CommandEntry> commands, int maxSuggestions)
        {
            string lowerInput = input.ToLower();
            var prefixMatches = new List<CommandEntry>();
            var containsMatches = new List<CommandEntry>();

            foreach (var command in commands)
            {
                string lowerCmd = command.Cmd.ToLower();
                if (lowerCmd.StartsWith(lowerInput))
                    prefixMatches.Add(command);
                else if (lowerCmd.Contains(lowerInput))
                    containsMatches.Add(command);
            }

            prefixMatches.Sort((a, b) => a.Cmd.Length.CompareTo(b.Cmd.Length));
            containsMatches.Sort((a, b) => a.Cmd.Length.CompareTo(b.Cmd.Length));

            var result = new List<Suggestion>();
            foreach (var command in prefixMatches)
                result.Add(new Suggestion { DisplayText = $"{command.Cmd} {command.Label}", InsertText = command.Cmd });
            foreach (var command in containsMatches)
                result.Add(new Suggestion { DisplayText = $"{command.Cmd} {command.Label}", InsertText = command.Cmd });

            if (result.Count > maxSuggestions)
                result.RemoveRange(maxSuggestions, result.Count - maxSuggestions);

            return result;
        }

        private static List<Suggestion> CollectNameSuggestions(string input, IReadOnlyList<CommandEntry> commands, int maxSuggestions)
        {
            string lowerInput = input.ToLower();
            var namePrefixMatches = new SortedDictionary<string, (string Label, string Description)>();
            var nameContainsMatches = new SortedDictionary<string, (string Label, string Description)>();
            var nameHasArgs = new Dictionary<string, bool>();

            foreach (var command in commands)
            {
                string name = GetCommandName(command.Cmd);
                string lowerName = name.ToLower();
                bool hasArgs = name.Length < command.Cmd.Length;

                if (lowerName.StartsWith(lowerInput))
                {
                    if (!namePrefixMatches.ContainsKey(name))
                        namePrefixMatches[name] = (command.Label, command.Description);
                    nameHasArgs[name] = hasArgs || (nameHasArgs.TryGetValue(name, out bool existing) && existing);
                }
                else if (lowerName.Contains(lowerInput))
                {
                    if (!nameContainsMatches.ContainsKey(name))
                        nameContainsMatches[name] = (command.Label, command.Description);
                    nameHasArgs[name] = hasArgs || (nameHasArgs.TryGetValue(name, out bool existing) && existing);
                }
            }

            var groupedResult = new List<Suggestion>();
            foreach (var pair in namePrefixMatches)
                groupedResult.Add(CreateNameSuggestion(pair.Key, pair.Value.Label, pair.Value.Description, nameHasArgs.TryGetValue(pair.Key, out bool hasArgs) && hasArgs));
            foreach (var pair in nameContainsMatches)
                groupedResult.Add(CreateNameSuggestion(pair.Key, pair.Value.Label, pair.Value.Description, nameHasArgs.TryGetValue(pair.Key, out bool hasArgs) && hasArgs));

            if (groupedResult.Count > maxSuggestions)
                groupedResult.RemoveRange(maxSuggestions, groupedResult.Count - maxSuggestions);

            return groupedResult;
        }

        private static Suggestion CreateNameSuggestion(string name, string label, string description, bool hasArgs)
        {
            string text = hasArgs ? name + "," : name;
            string note = GmCommandDescUtil.GetDescription(name);
            if (string.IsNullOrWhiteSpace(note))
                note = string.IsNullOrWhiteSpace(description) ? label : description;
            return new Suggestion
            {
                DisplayText = $"{name} {note}",
                InsertText = text,
            };
        }

        /// <summary>
        /// 参数暗字提示（逐行移植 Godot UpdateParamHint 的纯计算部分）：
        /// 输入以逗号结尾、逗号前是完整命令名、且 Luban 表有 param_hint 时，返回下一个待输入参数名；否则 null。
        /// </summary>
        public static string GetParamHintAt(string input, IReadOnlyList<CommandEntry> commands)
        {
            if (!input.EndsWith(","))
                return null;

            string namePart = GetCommandName(input);
            if (!IsExistingCommandName(namePart, commands))
                return null;

            string hint = GmCommandDescUtil.GetParamHint(namePart);
            if (string.IsNullOrWhiteSpace(hint))
                return null;

            string[] paramNames = hint.Split(',');
            int commaCount = 0;
            foreach (char c in input)
            {
                if (c == ',')
                    commaCount++;
            }
            int argIndex = commaCount - 1;

            if (argIndex < 0 || argIndex >= paramNames.Length)
                return null;

            return paramNames[argIndex].Trim();
        }
    }
}
