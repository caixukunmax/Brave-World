using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
        public override void _Ready()
        {
            SkillDataUtil.Load();
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
                Disconnected?.Invoke();
            }
        }

        public async void ConnectToServer()
        {
            GD.Print($"[NetworkManager] Connecting to {ServerHost}:{ServerPort}");

            if (_connected)
            {
                GD.Print("[NetworkManager] Already connected");
                return;
            }

            // 如果之前有失败的连接，先清理掉，确保每次重连都是干净状态
            _tcp?.DisconnectFromHost();
            _tcp = new StreamPeerTcp();

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
                GD.Print("[NetworkManager] Connected successfully");
                Connected?.Invoke();
                return;
            }

            GD.Print("[NetworkManager] Failed to connect, status: " + finalStatus);
            ConnectionError?.Invoke("无法连接到服务器");
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
