using Godot;

namespace ClinetCSharp.Editor.Remote
{
    /// <summary>
    /// 编辑器远程控制插件入口。
    /// 在编辑器内启动一个 HTTP 服务器，外部工具（AI / CLI 脚本）通过 REST API 操控编辑器。
    /// </summary>
    [Tool]
    public partial class EditorRemotePlugin : EditorPlugin
    {
        [Export] public int Port { get; set; } = 7788;
        [Export] public bool AutoStart { get; set; } = true;

        private EditorHttpServer _server;
        private EditorDock _dock;
        private RemotePanel _panel;

        public override void _EnterTree()
        {
            _server = new EditorHttpServer();
            AddChild(_server);

            // 注册所有 API handler
            SceneHandler.RegisterAll(_server);
            PropertyHandler.RegisterAll(_server);
            PlaybackHandler.RegisterAll(_server);
            ResourceHandler.RegisterAll(_server);

            // UI
            _panel = new RemotePanel();
            _panel.SetServer(_server);
            _panel.Port = Port;

            _dock = new EditorDock
            {
                Title = "Editor Remote",
                DefaultSlot = EditorDock.DockSlot.Bottom,
            };
            _dock.AddChild(_panel);
            AddDock(_dock);

            // 捕获 GD.Print 输出到缓冲区
            SetProcess(true);

            if (AutoStart)
            {
                _ServerStart(Port);
            }
        }

        public override void _ExitTree()
        {
            _server?.Stop();
            _server = null;

            if (_dock != null)
            {
                RemoveDock(_dock);
                _dock.QueueFree();
                _dock = null;
                _panel = null;
            }
        }

        public override void _Process(double delta)
        {
            // 轮询日志
            // 注意：Godot 4 的日志捕获比较复杂，这里通过 _PrintMessage 方式
        }

        internal void _ServerStart(int port)
        {
            var ok = _server.Start(port);
            if (ok)
            {
                GD.Print($"[EditorRemote] HTTP server started on http://localhost:{port}");
                PlaybackHandler.Log($"[EditorRemote] HTTP server started on port {port}");
            }
            else
            {
                GD.PrintErr($"[EditorRemote] Failed to start HTTP server on port {port}");
                PlaybackHandler.Log($"[EditorRemote] Failed to start on port {port}");
            }
            _panel?.RefreshStatus();
        }

        internal void _ServerStop()
        {
            _server?.Stop();
            GD.Print("[EditorRemote] HTTP server stopped");
            PlaybackHandler.Log("[EditorRemote] HTTP server stopped");
            _panel?.RefreshStatus();
        }
    }
}
