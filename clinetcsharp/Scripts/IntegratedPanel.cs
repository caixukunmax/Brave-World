using Godot;
using Protocol;
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 战斗信息面板 — 继承 DraggablePanel，只包含战斗日志业务逻辑。
    /// 拖拽/resize/最小化/关闭/位置保存防抖由基类处理。
    /// </summary>
    public partial class IntegratedPanel : DraggablePanel
    {
        private RichTextLabel _content;
        private NetworkManager _network;

        private const int MAX_LOG_LINES = 200;

        [Export] public float DefaultWidth { get; set; } = 400;
        [Export] public float DefaultHeight { get; set; } = 220;

        protected override void OnPanelInitialized()
        {
            // 发现内容节点
            _content = GetNodeOrNull<RichTextLabel>("VBoxContainer/Content");

            // 获取 NetworkManager 并订阅战斗日志
            _network = UiServices.GetNetworkManager(this);
            if (_network != null)
                _network.CombatLogNotify += OnCombatLogNotify;
        }

        public override void _ExitTree()
        {
            if (_network != null)
                _network.CombatLogNotify -= OnCombatLogNotify;
            base._ExitTree();
        }

        protected override void SavePosition()
        {
            if (_network == null) return;

            var req = new Game.UpdateUIPanelPosRequest
            {
                PosX = Position.X,
                PosY = Position.Y,
                Width = Size.X,
                Height = IsMinimized ? NormalHeight : Size.Y,
            };
            _network.SendPacket(MessageId.GameUpdateUiPanelPosReq, req);
        }

        protected override void OnClosed()
        {
            Visible = false;
        }

        #region Combat Log
        public void AppendCombatLogs(Game.CombatLogNotify notify)
        {
            if (_content == null) return;
            foreach (var e in notify.Entries)
            {
                string timeStr = TimeSpan.FromSeconds(e.Timestamp % 86400).ToString(@"hh\:mm\:ss");
                string line = FormatLogLine((int)e.LogType, timeStr, e.ActorName, e.TargetName, e.SkillName, e.Value, e.Extra);
                _content.AppendText(line + "\n");
            }
            TrimLogLines();
        }

        private string FormatLogLine(int logType, string time, string actor, string target, string skill, int value, string extra)
        {
            string color = logType switch
            {
                0 => "#FFD700",  // 战斗开始 - 金色
                1 => "#FFFFFF",  // 技能 - 白色
                2 => "#FFA500",  // 伤害 - 橙色
                3 => "#00FF00",  // 治疗 - 绿色
                4 => "#FF00FF",  // Buff - 紫色
                5 => "#AAAAAA",  // 闪避 - 灰色
                6 => "#FF0000",  // 死亡 - 红色
                7 => "#AAAAAA",  // 战斗结束 - 灰色
                _ => "#AAAAAA",
            };
            // 服务端已通过 CombatLogFormatter 格式化好完整句子，放在 extra 字段
            // 客户端只需显示时间 + 格式化文本
            string text = string.IsNullOrEmpty(extra) ? $"{actor}" : extra;
            return $"[color={color}]{time} {text}[/color]";
        }

        private void TrimLogLines()
        {
            if (_content == null) return;
            int lineCount = _content.GetLineCount();
            if (lineCount > MAX_LOG_LINES)
            {
                int removeCount = lineCount - MAX_LOG_LINES;
                var text = _content.Text;
                int pos = 0;
                for (int i = 0; i < text.Length && removeCount > 0; i++)
                {
                    if (text[i] == '\n') removeCount--;
                    pos = i + 1;
                }
                _content.Text = text.Substring(pos);
            }
        }

        private readonly Queue<Game.CombatLogNotify> _pendingLogs = new();

        private void OnCombatLogNotify(Game.CombatLogNotify notify)
        {
            lock (_pendingLogs)
                _pendingLogs.Enqueue(notify);
            CallDeferred(nameof(AppendCombatLogsDeferred));
        }

        private void AppendCombatLogsDeferred()
        {
            while (true)
            {
                Game.CombatLogNotify notify;
                lock (_pendingLogs)
                {
                    if (_pendingLogs.Count == 0) return;
                    notify = _pendingLogs.Dequeue();
                }
                AppendCombatLogs(notify);
            }
        }
        #endregion
    }
}
