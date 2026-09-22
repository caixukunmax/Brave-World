using Godot;

namespace ClinetCSharp.Editor.Remote
{
    /// <summary>
    /// Editor Remote 底部面板：显示服务器状态、端口号、启动/停止按钮、最近请求日志。
    /// </summary>
    [Tool]
    public partial class RemotePanel : VBoxContainer
    {
        public int Port { get; set; } = 7788;

        private EditorHttpServer _server;
        private Label _statusLabel;
        private Button _toggleButton;
        private LineEdit _portEdit;
        private RichTextLabel _logLabel;

        public void SetServer(EditorHttpServer server)
        {
            _server = server;
        }

        public override void _EnterTree()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill;
            SetAnchorsPreset(LayoutPreset.FullRect);

            // 顶栏：状态 + 端口 + 按钮
            var topBar = new HBoxContainer();
            topBar.AddThemeConstantOverride("separation", 12);

            _statusLabel = new Label { Text = "● 未启动", Modulate = new Color(1, 0.3f, 0.3f) };
            topBar.AddChild(_statusLabel);

            topBar.AddChild(new Label { Text = "端口:" });
            _portEdit = new LineEdit { Text = Port.ToString(), CustomMinimumSize = new Vector2(80, 0) };
            topBar.AddChild(_portEdit);

            _toggleButton = new Button { Text = "启动" };
            _toggleButton.Pressed += OnTogglePressed;
            topBar.AddChild(_toggleButton);

            AddChild(topBar);

            // 分隔
            AddChild(new HSeparator());

            // 日志区
            _logLabel = new RichTextLabel
            {
                BbcodeEnabled = false,
                ScrollFollowing = true,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            AddChild(_logLabel);

            // 底部提示
            var hint = new Label
            {
                Text = "API 文档: GET /api/editor/info",
                Modulate = new Color(0.6f, 0.6f, 0.6f)
            };
            AddChild(hint);

            RefreshStatus();
        }

        private void OnTogglePressed()
        {
            if (_server == null) return;

            if (_server.IsRunning)
            {
                _server.Stop();
            }
            else
            {
                if (int.TryParse(_portEdit.Text, out var port))
                {
                    _server.Start(port);
                }
            }
            RefreshStatus();
        }

        public void RefreshStatus()
        {
            if (_server == null) return;

            if (_server.IsRunning)
            {
                _statusLabel.Text = $"● 运行中 (端口 {_server.Port})";
                _statusLabel.Modulate = new Color(0.3f, 1, 0.3f);
                _toggleButton.Text = "停止";
            }
            else
            {
                _statusLabel.Text = "● 未启动";
                _statusLabel.Modulate = new Color(1, 0.3f, 0.3f);
                _toggleButton.Text = "启动";
            }
        }

        public void AppendLog(string message)
        {
            if (_logLabel != null)
            {
                _logLabel.Text += $"[{Time.GetTimeStringFromSystem()}] {message}\n";
            }
        }
    }
}
