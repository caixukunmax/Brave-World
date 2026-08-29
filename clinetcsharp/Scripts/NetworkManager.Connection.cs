using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
        // ---- 开发期自动重连 ----
        // 断线或连不上时，按固定间隔自动重试连接，直到成功。
        // 仅在编辑器/dev 环境启用（生产版重连策略属产品决策，暂不在此改动）。
        private bool _autoReconnectEnabled;
        private bool _connecting;
        private double _reconnectTimer;
        private const double ReconnectInterval = 1.5;

        public override void _Ready()
        {
            _autoReconnectEnabled = OS.HasFeature("editor_build");
            SkillDataUtil.Load();
            TerrainConfigUtil.Load();
            DecorationConfigUtil.Load();
            GmCommandDescUtil.Load();
            GD.Print("[NetworkManager] _ready() initializing...");
            _tcp = new StreamPeerTcp();
            GD.Print("[NetworkManager] Initialized");
        }

        public override void _Process(double delta)
        {
            _timestampDirty = true;

            if (_tcp == null)
                return;

            var status = _tcp.GetStatus();
            if (status == StreamPeerTcp.Status.Connected)
            {
                _tcp.Poll();
                ReadPackets();

                if (_connected)
                {
                    _heartbeatTimer += delta;
                    if (_heartbeatTimer >= _heartbeatInterval)
                    {
                        _heartbeatTimer = 0.0;
                        SendHeartbeat();
                    }
                }

                return;
            }

            if (status == StreamPeerTcp.Status.Connecting)
            {
                _tcp.Poll();
                return;
            }

            if ((status == StreamPeerTcp.Status.None || status == StreamPeerTcp.Status.Error) && _connected)
            {
                _connected = false;
                ResetDecodeState();
                Disconnected?.Invoke();
            }

            // 未连接（连不上 / 已断开）→ 开发期自动重连，天然等待托管拉起的服务器就绪
            if (_autoReconnectEnabled && !_connected && !_connecting)
            {
                _reconnectTimer += delta;
                if (_reconnectTimer >= ReconnectInterval)
                {
                    _reconnectTimer = 0.0;
                    GD.Print("[NetworkManager] 自动重连：尝试重新连接服务器...");
                    ConnectToServer();
                }
            }
        }

        /// <summary>重置 TCP 包重组状态：断线/重连时清掉半截包残留，避免污染新连接或按垃圾长度撑爆缓冲。</summary>
        private void ResetDecodeState()
        {
            _bufferOffset = 0;
            _bufferCount = 0;
            _expectedLength = -1;
        }

        public async void ConnectToServer()
        {
            GD.Print($"[NetworkManager] Connecting to {ServerHost}:{ServerPort}");

            if (_connected)
            {
                GD.Print("[NetworkManager] Already connected");
                return;
            }

            // 已有一次连接在途，避免自动重连与手动触发并发
            if (_connecting)
                return;

            _connecting = true;
            try
            {
                // 如果之前有失败的连接，先清理掉，确保每次重连都是干净状态
                _tcp?.DisconnectFromHost();
                _tcp = new StreamPeerTcp();
                ResetDecodeState();

                var err = _tcp.ConnectToHost(ServerHost, ServerPort);
                if (err != Error.Ok)
                {
                    GD.Print("[NetworkManager] Connection failed with error: " + err);
                    ConnectionError?.Invoke("连接服务器失败: " + err);
                    return;
                }

                GD.Print("[NetworkManager] Waiting for connection...");

                int attempts = 0;
                while (_tcp.GetStatus() == StreamPeerTcp.Status.Connecting && attempts < 100)
                {
                    _tcp.Poll();
                    await ToSignal(GetTree().CreateTimer(0.05), "timeout");
                    attempts++;
                }

                var finalStatus = _tcp.GetStatus();
                GD.Print($"[NetworkManager] Connection status after {attempts} attempts: {finalStatus}");

                if (finalStatus == StreamPeerTcp.Status.Connected)
                {
                    _connected = true;
                    _reconnectTimer = 0.0;
                    GD.Print("[NetworkManager] Connected successfully");
                    Connected?.Invoke();
                    return;
                }

                GD.Print("[NetworkManager] Failed to connect, status: " + finalStatus);
                ConnectionError?.Invoke("无法连接到服务器");
            }
            finally
            {
                _connecting = false;
            }
        }

        public void DisconnectFromServer()
        {
            _tcp?.DisconnectFromHost();
            _connected = false;
            _heartbeatTimer = 0.0;
            _bufferOffset = 0;
            _bufferCount = 0;
            _expectedLength = -1;
        }

        public bool IsServerConnected()
        {
            return _connected && _tcp != null && _tcp.GetStatus() == StreamPeerTcp.Status.Connected;
        }

        private void SendHeartbeat()
        {
            if (!IsServerConnected())
                return;

            var req = new Gateway.HeartbeatRequest
            {
                ClientTime = (ulong)Time.GetUnixTimeFromSystem(),
            };
            SendPacket(MessageId.GatewayHeartbeatReq, req);
        }
    }
}
