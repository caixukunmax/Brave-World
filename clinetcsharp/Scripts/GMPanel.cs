using Godot;
using System.Collections.Generic;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// GM 调试面板 - 发送 GM 命令到服务器
    /// 按 F2 开关
    /// </summary>
    public partial class GMPanel : CanvasLayer
    {
        private Panel _panel;
        private VBoxContainer _vbox;
        private OptionButton _commandOption;
        private LineEdit _argsEdit;
        private Button _execBtn;
        private RichTextLabel _logOutput;
        private bool _isVisible = false;

        // 预设 GM 命令
        private static readonly string[] GM_COMMANDS = new string[]
        {
            "additem",
        };
        private static readonly string[] GM_COMMAND_HINTS = new string[]
        {
            "添加道具 (物品ID:数量)",
        };

        public override void _Ready()
        {
            BuildUI();
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F2)
            {
                Toggle();
                GetViewport().SetInputAsHandled();
            }
        }

        private void BuildUI()
        {
            var vpSize = GetViewport().GetVisibleRect().Size;
            var pw = 360f;
            var ph = 300f;

            _panel = new Panel();
            _panel.Position = new Vector2(vpSize.X - pw - 10, 60);
            _panel.Size = new Vector2(pw, ph);

            _vbox = new VBoxContainer();
            _vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _vbox.OffsetLeft = 8;
            _vbox.OffsetTop = 8;
            _vbox.OffsetRight = -8;
            _vbox.OffsetBottom = -8;

            // 标题
            var title = new Label
            {
                Text = "GM 调试面板 (F2)",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            title.AddThemeFontSizeOverride("font_size", 14);
            _vbox.AddChild(title);

            // 命令选择
            var cmdBox = new HBoxContainer();
            cmdBox.AddChild(new Label { Text = "命令:", CustomMinimumSize = new Vector2(50, 0) });

            _commandOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            for (int i = 0; i < GM_COMMANDS.Length; i++)
                _commandOption.AddItem($"{GM_COMMANDS[i]} - {GM_COMMAND_HINTS[i]}", i);
            _commandOption.ItemSelected += idx => UpdateArgsHint();
            cmdBox.AddChild(_commandOption);
            _vbox.AddChild(cmdBox);

            // 参数输入
            var argsBox = new HBoxContainer();
            argsBox.AddChild(new Label { Text = "参数:", CustomMinimumSize = new Vector2(50, 0) });
            _argsEdit = new LineEdit
            {
                PlaceholderText = "例: 1001:5",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            argsBox.AddChild(_argsEdit);
            _vbox.AddChild(argsBox);

            // 执行按钮
            _execBtn = new Button
            {
                Text = "执行",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _execBtn.Pressed += OnExecPressed;
            _vbox.AddChild(_execBtn);

            // 日志输出
            _logOutput = new RichTextLabel
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                BbcodeEnabled = true,
                ScrollActive = true,
                ScrollFollowing = true,
            };
            _vbox.AddChild(_logOutput);

            _panel.AddChild(_vbox);
            AddChild(_panel);
            _panel.Visible = false;
        }

        private void Toggle()
        {
            _isVisible = !_isVisible;
            _panel.Visible = _isVisible;
        }

        private void UpdateArgsHint()
        {
            int idx = (int)_commandOption.GetSelectedId();
            if (idx >= 0 && idx < GM_COMMAND_HINTS.Length)
                _argsEdit.PlaceholderText = GM_COMMAND_HINTS[idx];
        }

        private void OnExecPressed()
        {
            int cmdIdx = (int)_commandOption.GetSelectedId();
            if (cmdIdx < 0 || cmdIdx >= GM_COMMANDS.Length) return;

            string command = GM_COMMANDS[cmdIdx];
            string args = _argsEdit.Text.StripEdges();

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected())
            {
                AppendLog("[color=red]未连接服务器[/color]");
                return;
            }

            var req = new Game.GmCommandRequest { Command = command, Args = args };
            nm.SendPacket(MessageId.GameGmReq, req);

            AppendLog($"[color=cyan]> {command} {args}[/color]");
            _argsEdit.Text = "";
        }

        public void OnGmResponse(Game.GmCommandResponse rsp)
        {
            string color = rsp.Code == Common.ErrorCode.Success ? "green" : "red";
            AppendLog($"[color={color}]{rsp.Message}[/color]");

            // 如果返回了背包数据，更新 InventoryManager
            if (rsp.Code == Common.ErrorCode.Success && rsp.Items.Count > 0)
            {
                var inv = GetNodeOrNull<InventoryManager>("/root/Main/InventoryManager");
                if (inv != null)
                    inv.UpdateFromProto(rsp.Items);
            }
        }

        private void AppendLog(string bbcode)
        {
            _logOutput.AppendText(bbcode + "\n");
        }
    }
}
