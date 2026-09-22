using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Rendering;
using UnityClientSharp.Net;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// GM 面板动作层 — 移植自 Godot GMPanel.Actions.cs：
    /// 执行命令（含本地 story 命令）、GmResponse 处理（颜色日志 / TELEPORT 瞬移 / 背包回写）、
    /// 自动补全（逻辑在 <see cref="GmCommandSuggester"/>）、参数提示、命令历史（Unity 增强）。
    /// 离线行为对齐 Godot ExecuteGmCommand：未连接时只在响应区打红色"未连接服务器"，不发包。
    /// </summary>
    public partial class GMPanelHud
    {
        private void OnExecPressed()
        {
            // 对齐 Godot：下拉可见时回车/执行 = 选中第一条候选
            if (_suggestPanel.gameObject.activeSelf && _suggestItems.Count > 0)
            {
                OnSuggestionSelected(0);
                return;
            }

            string commandLine = _cmdEdit.text.Trim();
            if (commandLine == "")
                return;

            ExecuteGmCommand(commandLine);
            _cmdEdit.text = "";
        }

        private void ExecuteGmCommand(string commandLine)
        {
            if (commandLine == "")
                return;

            PushHistory(commandLine);

            // 客户端本地 GM 命令：剧情测试
            if (TryHandleLocalGmCommand(commandLine))
            {
                AppendLog("cyan", $"> {commandLine}");
                return;
            }

            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected())
            {
                AppendLog("red", "未连接服务器");
                return;
            }

            nm.SendPacket(Protocol.MessageId.GameGmReq, new Game.GmCommandRequest
            {
                Command = commandLine,
                Args = "",
            });
            AppendLog("cyan", $"> {commandLine}");
        }

        /// <summary>本地 story 命令（对齐 Godot TryHandleLocalGmCommand；StoryPanel 未迁移，恒报未找到）。</summary>
        private bool TryHandleLocalGmCommand(string commandLine)
        {
            string lower = commandLine.ToLower();
            if (!lower.StartsWith("story,"))
                return false;

            string arg = commandLine.Substring("story,".Length).Trim();
            if (!int.TryParse(arg, out int chapterId))
            {
                AppendLog("red", "用法：story,章节ID");
                return true;
            }

            // StoryPanel 尚未迁移到 Unity，对齐 Godot panel == null 分支
            AppendLog("red", $"剧情面板未找到（章节 {chapterId} 未播放）");
            return true;
        }

        private void OnGmResponse(Game.GmCommandResponse response)
        {
            AppendLog(response.Code == Common.ErrorCode.Success ? "green" : "red", response.Message);

            if (response.Code != Common.ErrorCode.Success)
                return;

            HandleTeleportResponse(response.Message);
            HandleInventoryResponse(response);
        }

        /// <summary>对齐 Godot HandleTeleportResponse：TELEPORT:/RETURN: 响应 → 玩家瞬移。</summary>
        private void HandleTeleportResponse(string message)
        {
            if (!message.StartsWith("TELEPORT:") && !message.StartsWith("RETURN:"))
                return;

            var parts = message.Split(':');
            if (parts.Length != 3 ||
                !int.TryParse(parts[1], out int targetX) ||
                !int.TryParse(parts[2], out int targetY))
            {
                return;
            }

            var player = PlayerEntity.Instance;
            if (player == null)
                return;

            player.TeleportToGrid(new Vector2Int(targetX, targetY));
            AppendLog("cyan", $"已瞬移到 ({targetX}, {targetY})");
        }

        /// <summary>对齐 Godot HandleInventoryResponse：响应带物品时全量回写背包。</summary>
        private void HandleInventoryResponse(Game.GmCommandResponse response)
        {
            if (response.Items.Count == 0)
                return;

            var nm = NetworkManager.Instance;
            if (nm != null)
                nm.CachedItems = new List<Game.ItemInfo>(response.Items);

            // 经 GamePanelManager 找背包面板的数据层（对齐 Godot UiServices.GetInventoryManager）
            var inventory = GamePanelManager.Instance != null
                ? GamePanelManager.Instance.GetPanel<InventoryPanelHud>()
                : null;
            inventory?.Manager?.UpdateFromProto(response.Items);
        }

        // ============ 响应日志（Unity 增强：Godot AppendLog 只 GD.Print） ============

        private void AppendLog(string color, string message)
        {
            Debug.Log($"[GM] {message}");

            if (_logContent == null)
                return;

            string hex = color switch
            {
                "green" => "#4CFF4C",
                "red" => "#FF6666",
                "cyan" => "#66CCFF",
                _ => "#CCCCCC",
            };
            var tmp = CreateText(_logContent, "Log", $"<color={hex}>{message}</color>", 12, Color.white);
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.richText = true;

            // 只保留最近 MaxLogLines 行
            while (_logContent.childCount > MaxLogLines)
                Destroy(_logContent.GetChild(0).gameObject);

            // 滚动到底部
            Canvas.ForceUpdateCanvases();
            if (_logScroll != null)
                _logScroll.verticalNormalizedPosition = 0f;
        }

        // ============ 命令历史（Unity 增强） ============

        private void PushHistory(string commandLine)
        {
            if (_history.Count == 0 || _history[_history.Count - 1] != commandLine)
            {
                _history.Add(commandLine);
                if (_history.Count > MaxHistory)
                    _history.RemoveAt(0);
            }
            _historyIndex = -1;
        }

        private void NavigateHistory(int direction)
        {
            if (_history.Count == 0)
                return;

            if (_historyIndex < 0)
                _historyIndex = direction < 0 ? _history.Count - 1 : -1;
            else
                _historyIndex = Mathf.Clamp(_historyIndex + direction, 0, _history.Count - 1);

            if (_historyIndex < 0)
                return;

            _suppressSuggestionUpdate = true;
            _cmdEdit.text = _history[_historyIndex];
            _cmdEdit.MoveTextEnd(false);
        }

        // ============ 自动补全与参数提示（对齐 Godot OnCmdTextChanged 及之后的方法） ============

        private void OnCmdTextChanged(string text)
        {
            string input = text.Trim();
            UpdateParamHint(input);

            if (_suppressSuggestionUpdate)
            {
                _suppressSuggestionUpdate = false;
                return;
            }

            if (input == "")
            {
                HideSuggestions();
                return;
            }

            var matches = GmCommandSuggester.CollectSuggestions(input, AllCommands());
            if (matches.Count == 0)
            {
                HideSuggestions();
                return;
            }

            _suggestItems.Clear();
            foreach (Transform child in _suggestPanel)
                Destroy(child.gameObject);

            foreach (var suggestion in matches)
            {
                _suggestItems.Add(suggestion);
                BuildSuggestionButton(suggestion, _suggestItems.Count - 1);
            }

            _suggestPanel.gameObject.SetActive(true);
        }

        /// <summary>全部已配置命令的纯数据投影（供 GmCommandSuggester）。</summary>
        private List<GmCommandSuggester.CommandEntry> AllCommands()
        {
            var result = new List<GmCommandSuggester.CommandEntry>();
            foreach (var group in _groups)
            {
                foreach (var cmd in group.Commands)
                    result.Add(new GmCommandSuggester.CommandEntry(cmd.Label, cmd.Cmd, cmd.Description));
            }
            return result;
        }

        private void UpdateParamHint(string input)
        {
            string hint = GmCommandSuggester.GetParamHintAt(input, AllCommands());
            if (hint == null)
            {
                if (_cmdHint != null) _cmdHint.gameObject.SetActive(false);
                return;
            }

            _cmdHint.text = $"参数：{hint}";
            _cmdHint.gameObject.SetActive(true);
        }

        private void HideSuggestions()
        {
            if (_suggestPanel != null) _suggestPanel.gameObject.SetActive(false);
            _suggestItems.Clear();
        }

        private void OnSuggestionSelected(int index)
        {
            if (index < 0 || index >= _suggestItems.Count)
                return;

            _suppressSuggestionUpdate = true;
            _cmdEdit.text = _suggestItems[index].InsertText;
            _cmdEdit.ActivateInputField();
            _cmdEdit.MoveTextEnd(false);
            if (_cmdHint != null) _cmdHint.gameObject.SetActive(false);
            HideSuggestions();
        }
    }
}
