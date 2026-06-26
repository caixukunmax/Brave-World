using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class GMPanel
    {
        private void OnExecPressed()
        {
            if (_suggestPanel.Visible && _suggestItems.Count > 0)
            {
                OnSuggestionSelected(0);
                return;
            }

            string commandLine = _cmdEdit.Text.StripEdges();
            if (commandLine == "")
                return;

            if (_network == null || !_network.IsServerConnected())
            {
                AppendLog("[color=red]未连接服务器[/color]");
                return;
            }

            var request = new Game.GmCommandRequest
            {
                Command = commandLine,
                Args = "",
            };

            _network.SendPacket(MessageId.GameGmReq, request);
            AppendLog($"[color=cyan]> {commandLine}[/color]");
            _cmdEdit.Text = "";
        }

        private void OnGmResponse(Game.GmCommandResponse response)
        {
            string color = response.Code == Common.ErrorCode.Success ? "green" : "red";
            AppendLog($"[color={color}]{response.Message}[/color]");

            if (response.Code != Common.ErrorCode.Success)
                return;

            HandleTeleportResponse(response.Message);
            HandleInventoryResponse(response);
        }

        private void HandleTeleportResponse(string message)
        {
            if (!message.StartsWith("TELEPORT:"))
                return;

            var parts = message.Split(':');
            if (parts.Length != 3 ||
                !int.TryParse(parts[1], out int targetX) ||
                !int.TryParse(parts[2], out int targetY))
            {
                return;
            }

            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            if (player == null)
                return;

            player.TeleportToGrid(targetX, targetY);
            AppendLog($"[color=cyan]已瞬移到 ({targetX}, {targetY})[/color]");
        }

        private void HandleInventoryResponse(Game.GmCommandResponse response)
        {
            if (response.Items.Count == 0)
                return;

            var inventoryManager = UiServices.GetInventoryManager(this);
            inventoryManager?.UpdateFromProto(response.Items);
        }

        private void AppendLog(string bbcode)
        {
            GD.Print($"[GM] {bbcode}");
        }

        private void OnCmdTextChanged(string text)
        {
            string input = text.StripEdges();
            UpdateParamHint(input);

            if (_suppressSuggestionUpdate)
            {
                _suppressSuggestionUpdate = false;
                return;
            }

            if (input == "")
            {
                _suggestPanel.Hide();
                _suggestItems.Clear();
                return;
            }

            var matches = CollectSuggestions(input);
            if (matches.Count == 0)
            {
                _suggestPanel.Hide();
                _suggestItems.Clear();
                return;
            }

            _suggestItems.Clear();
            foreach (var child in _suggestPanel.GetChildren())
                child.QueueFree();

            foreach (var suggestion in matches)
            {
                _suggestItems.Add(suggestion);
                BuildSuggestionButton(suggestion, _suggestItems.Count - 1);
            }

            _suggestPanel.Show();
        }

        private void UpdateParamHint(string input)
        {
            if (!input.EndsWith(","))
            {
                _cmdHint.Visible = false;
                return;
            }

            string namePart = GetCommandName(input);
            if (!IsExistingCommandName(namePart))
            {
                _cmdHint.Visible = false;
                return;
            }

            string hint = GmCommandDescUtil.GetParamHint(namePart);
            if (string.IsNullOrWhiteSpace(hint))
            {
                _cmdHint.Visible = false;
                return;
            }

            string[] paramNames = hint.Split(',');
            int commaCount = 0;
            foreach (char c in input)
            {
                if (c == ',')
                    commaCount++;
            }
            int argIndex = commaCount - 1;

            if (argIndex < 0 || argIndex >= paramNames.Length)
            {
                _cmdHint.Visible = false;
                return;
            }

            _cmdHint.Text = paramNames[argIndex].Trim();
            PositionHintLabelAfterLastComma();
            _cmdHint.Visible = true;
        }

        private void PositionHintLabelAfterLastComma()
        {
            Font font = _cmdEdit.GetThemeFont("font");
            int fontSize = _cmdEdit.GetThemeFontSize("font_size");
            int lastCommaIndex = _cmdEdit.Text.LastIndexOf(',');
            string prefix = lastCommaIndex >= 0 ? _cmdEdit.Text.Substring(0, lastCommaIndex + 1) : _cmdEdit.Text;
            Vector2 textSize = font.GetStringSize(prefix, HorizontalAlignment.Left, -1, fontSize);
            const float HintOffsetX = 4f;
            float x = _cmdEdit.GlobalPosition.X + textSize.X + HintOffsetX;
            float y = _cmdEdit.GlobalPosition.Y + (_cmdEdit.Size.Y - textSize.Y) / 2;
            _cmdHint.GlobalPosition = new Vector2(x, y);
        }

        private System.Collections.Generic.List<Suggestion> CollectSuggestions(string input)
        {
            const int MaxSuggestions = 8;

            if (input.Contains(","))
            {
                string namePart = GetCommandName(input);
                if (IsExistingCommandName(namePart))
                    return CollectFullCommandSuggestions(input, MaxSuggestions);

                return CollectNameSuggestions(namePart, MaxSuggestions);
            }

            return CollectNameSuggestions(input, MaxSuggestions);
        }

        private bool IsExistingCommandName(string name)
        {
            string lowerName = name.ToLower();
            foreach (var group in _groups)
            {
                foreach (var command in group.Commands)
                {
                    if (GetCommandName(command.Cmd).ToLower() == lowerName)
                        return true;
                }
            }
            return false;
        }

        private System.Collections.Generic.List<Suggestion> CollectFullCommandSuggestions(string input, int maxSuggestions)
        {
            string lowerInput = input.ToLower();
            var prefixMatches = new System.Collections.Generic.List<GmCommand>();
            var containsMatches = new System.Collections.Generic.List<GmCommand>();

            foreach (var group in _groups)
            {
                foreach (var command in group.Commands)
                {
                    string lowerCmd = command.Cmd.ToLower();
                    if (lowerCmd.StartsWith(lowerInput))
                        prefixMatches.Add(command);
                    else if (lowerCmd.Contains(lowerInput))
                        containsMatches.Add(command);
                }
            }

            prefixMatches.Sort((a, b) => a.Cmd.Length.CompareTo(b.Cmd.Length));
            containsMatches.Sort((a, b) => a.Cmd.Length.CompareTo(b.Cmd.Length));

            var result = new System.Collections.Generic.List<Suggestion>();
            foreach (var command in prefixMatches)
                result.Add(new Suggestion { DisplayText = $"{command.Cmd} {command.Label}", InsertText = command.Cmd });
            foreach (var command in containsMatches)
                result.Add(new Suggestion { DisplayText = $"{command.Cmd} {command.Label}", InsertText = command.Cmd });

            if (result.Count > maxSuggestions)
                result.RemoveRange(maxSuggestions, result.Count - maxSuggestions);

            return result;
        }

        private System.Collections.Generic.List<Suggestion> CollectNameSuggestions(string input, int maxSuggestions)
        {
            string lowerInput = input.ToLower();
            var namePrefixMatches = new System.Collections.Generic.SortedDictionary<string, (string Label, string Description)>();
            var nameContainsMatches = new System.Collections.Generic.SortedDictionary<string, (string Label, string Description)>();
            var nameHasArgs = new System.Collections.Generic.Dictionary<string, bool>();

            foreach (var group in _groups)
            {
                foreach (var command in group.Commands)
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
            }

            var groupedResult = new System.Collections.Generic.List<Suggestion>();
            foreach (var pair in namePrefixMatches)
                groupedResult.Add(CreateNameSuggestion(pair.Key, pair.Value.Label, pair.Value.Description, nameHasArgs.TryGetValue(pair.Key, out bool hasArgs) && hasArgs));
            foreach (var pair in nameContainsMatches)
                groupedResult.Add(CreateNameSuggestion(pair.Key, pair.Value.Label, pair.Value.Description, nameHasArgs.TryGetValue(pair.Key, out bool hasArgs) && hasArgs));

            if (groupedResult.Count > maxSuggestions)
                groupedResult.RemoveRange(maxSuggestions, groupedResult.Count - maxSuggestions);

            return groupedResult;
        }

        private string GetCommandName(string cmd)
        {
            int commaIndex = cmd.IndexOf(',');
            return commaIndex >= 0 ? cmd.Substring(0, commaIndex) : cmd;
        }

        private Suggestion CreateNameSuggestion(string name, string label, string description, bool hasArgs)
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

        private void OnSuggestionSelected(int index)
        {
            if (index < 0 || index >= _suggestItems.Count)
                return;

            _suppressSuggestionUpdate = true;
            _cmdEdit.Text = _suggestItems[index].InsertText;
            _cmdEdit.GrabFocus();
            _cmdEdit.CaretColumn = _cmdEdit.Text.Length;
            _cmdHint.Hide();
            _suggestPanel.Hide();
        }

        private void OnCmdEditGuiInput(InputEvent @event)
        {
            if (@event is not InputEventKey keyEvent || !keyEvent.Pressed)
                return;

            if (keyEvent.Keycode == Key.Escape && _suggestPanel.Visible)
            {
                _suggestPanel.Hide();
                AcceptEvent();
            }
        }
    }
}
