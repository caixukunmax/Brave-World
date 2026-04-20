using Godot;
using Protocol;
using System;

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

        protected override void OnPanelReady()
        {
            // 发现内容节点
            _content = GetNodeOrNull<RichTextLabel>("VBoxContainer/Content");

            // 面板样式
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0.85f),
                BorderColor = new Color(0.2f, 0.2f, 0.2f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
            };
            AddThemeStyleboxOverride("panel", style);

            // 标题栏样式
            var titleBar = GetNodeOrNull<PanelContainer>("VBoxContainer/TitleBar");
            if (titleBar != null)
            {
                titleBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                });
            }

            // 获取 NetworkManager 并订阅战斗日志
            var tree = GetTree();
            if (tree != null)
            {
                foreach (var child in tree.Root.GetChildren())
                {
                    if (child is NetworkManager nm)
                    {
                        _network = nm;
                        break;
                    }
                }
                if (_network == null)
                    _network = tree.Root.GetNodeOrNull<NetworkManager>("NetworkManager");
                if (_network != null)
                    _network.CombatLogNotify += OnCombatLogNotify;
            }
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
            return logType switch
            {
                0 => $"[color=#FFD700]{time} [{actor}] 与 [{target}] {extra}[/color]",
                1 => $"[color=#FFFFFF]{time} [{actor}] 使用了 {skill}[/color]",
                2 => $"[color=#FFA500]{time} [{actor}] 对 [{target}] 造成了 {value} 点伤害[/color]",
                3 => $"[color=#00FF00]{time} [{actor}] 恢复了 {value} 点生命[/color]",
                5 => $"[color=#AAAAAA]{time} [{actor}] 的攻击打空了 ({extra})[/color]",
                6 => $"[color=#FF0000]{time} [{actor}] {extra}[/color]",
                7 => $"[color=#AAAAAA]{time} [{actor}] {extra}[/color]",
                _ => $"[color=#AAAAAA]{time} [{actor}] {extra}[/color]",
            };
        }

        private void TrimLogLines()
        {
            if (_content == null) return;
            var text = _content.Text;
            int lines = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') lines++;
            }
            if (lines > MAX_LOG_LINES)
            {
                int removeCount = lines - MAX_LOG_LINES;
                int pos = 0;
                for (int i = 0; i < text.Length && removeCount > 0; i++)
                {
                    if (text[i] == '\n') removeCount--;
                    pos = i + 1;
                }
                _content.Text = text.Substring(pos);
            }
        }

        private void OnCombatLogNotify(Game.CombatLogNotify notify)
        {
            CallDeferred(nameof(AppendCombatLogs), notify);
        }
        #endregion
    }
}
