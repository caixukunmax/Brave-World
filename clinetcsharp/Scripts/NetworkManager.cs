using System;
using System.Linq;
using Google.Protobuf;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 网络管理器 - 处理TCP连接和Protobuf消息收发
    /// Wire Format: [4 bytes LE 长度][Protobuf Packet 二进制]
    /// </summary>
    public partial class NetworkManager : Node
    {
        [Signal]
        public delegate void ConnectedEventHandler();

        [Signal]
        public delegate void DisconnectedEventHandler();

        [Signal]
        public delegate void PacketReceivedEventHandler(int msgId);

        [Signal]
        public delegate void ConnectionErrorEventHandler(string error);

        public const string ServerHost = "127.0.0.1";
        public const int ServerPort = 8889;

        private StreamPeerTcp _tcp;
        private bool _connected = false;
        private byte[] _readBuffer = new byte[0];
        private int _expectedLength = -1;
        private uint _sessionCounter = 1;

        // 登录相关缓存数据
        public string AccountToken { get; set; } = "";
        public uint AccountId { get; set; } = 0;
        public Godot.Collections.Array Servers { get; set; } = new Godot.Collections.Array();
        public uint LastServerId { get; set; } = 0;
        public string LastRoleName { get; set; } = "";

        // 选服相关缓存数据
        public string GatewayToken { get; set; } = "";
        public Godot.Collections.Array Roles { get; set; } = new Godot.Collections.Array();
        public uint MaxRoleCount { get; set; } = 3;
        public uint ServerTime { get; set; } = 0;

        // 进入游戏后缓存的角色完整信息
        public Game.FullRoleInfo CachedRoleInfo { get; set; } = null;

        // 最后收到的响应原始数据（供场景解析特定类型）
        private byte[] _lastPayload = new byte[0];
        private int _lastMsgId = 0;
        public int LastMsgId => _lastMsgId;
        public byte[] GetLastPayload() => _lastPayload;

        public override void _Ready()
        {
            GD.Print("[NetworkManager] _ready() initializing...");
            _tcp = new StreamPeerTcp();
            GD.Print("[NetworkManager] Initialized");
        }

        public override void _Process(double _delta)
        {
            if (_tcp == null)
                return;

            var status = _tcp.GetStatus();
            if (status == StreamPeerTcp.Status.Connected)
            {
                _tcp.Poll();
                ReadPackets();
            }
            else if (status == StreamPeerTcp.Status.Connecting)
            {
                _tcp.Poll();
            }
            else if (status == StreamPeerTcp.Status.None || status == StreamPeerTcp.Status.Error)
            {
                if (_connected)
                {
                    _connected = false;
                    EmitSignal(SignalName.Disconnected);
                }
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

            if (_tcp == null)
                _tcp = new StreamPeerTcp();

            var err = _tcp.ConnectToHost(ServerHost, ServerPort);
            if (err != Error.Ok)
            {
                GD.Print("[NetworkManager] Connection failed with error: " + err);
                EmitSignal(SignalName.ConnectionError, "连接服务器失败: " + err);
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
                EmitSignal(SignalName.Connected);
            }
            else
            {
                GD.Print("[NetworkManager] Failed to connect, status: " + finalStatus);
                EmitSignal(SignalName.ConnectionError, "无法连接到服务器");
            }
        }

        public void DisconnectFromServer()
        {
            if (_tcp != null)
                _tcp.DisconnectFromHost();
            _connected = false;
            _readBuffer = new byte[0];
            _expectedLength = -1;
        }

        public bool IsServerConnected()
        {
            return _connected && _tcp != null && _tcp.GetStatus() == StreamPeerTcp.Status.Connected;
        }

        /// <summary>
        /// 发送 Protobuf 消息
        /// 包装为 Packet { msg_id, session, data } 后序列化发送
        /// </summary>
        public bool SendPacket(MessageId msgId, IMessage data)
        {
            GD.Print($"[NetworkManager] SendPacket called, msgId: {msgId}, connected: {IsServerConnected()}");

            if (!IsServerConnected())
            {
                GD.PushError("[NetworkManager] 未连接到服务器，无法发送消息");
                return false;
            }

            var packet = new Common.Packet
            {
                MsgId = (uint)msgId,
                Session = _sessionCounter++,
                Data = ByteString.CopyFrom(data.ToByteArray()),
                Timestamp = (ulong)Time.GetUnixTimeFromSystem()
            };

            var body = packet.ToByteArray();
            var header = BitConverter.GetBytes((uint)body.Length);
            if (!BitConverter.IsLittleEndian)
                System.Array.Reverse(header);

            var combined = new byte[header.Length + body.Length];
            header.CopyTo(combined, 0);
            body.CopyTo(combined, header.Length);

            GD.Print($"[NetworkManager] Sending {combined.Length} bytes (protobuf Packet, msgId={msgId})");

            var err = _tcp.PutData(combined);
            if (err != Error.Ok)
            {
                GD.PushError($"[NetworkManager] PutData failed: {err}");
                EmitSignal(SignalName.ConnectionError, "发送消息失败: " + err);
                return false;
            }
            GD.Print("[NetworkManager] Packet sent successfully");
            return true;
        }

        private void ReadPackets()
        {
            int available = _tcp.GetAvailableBytes();
            if (available > 0)
            {
                var chunk = _tcp.GetPartialData(available);
                if (chunk[0].AsInt32() == (int)Error.Ok)
                {
                    var chunkData = chunk[1].AsByteArray();
                    var newBuffer = new byte[_readBuffer.Length + chunkData.Length];
                    _readBuffer.CopyTo(newBuffer, 0);
                    chunkData.CopyTo(newBuffer, _readBuffer.Length);
                    _readBuffer = newBuffer;
                }
            }

            int maxPackets = 10;
            int packetsProcessed = 0;

            while (packetsProcessed < maxPackets)
            {
                if (_expectedLength < 0)
                {
                    if (_readBuffer.Length < 4)
                        return;
                    var headerBytes = _readBuffer.Take(4).ToArray();
                    if (!BitConverter.IsLittleEndian)
                        System.Array.Reverse(headerBytes);
                    _expectedLength = (int)BitConverter.ToUInt32(headerBytes, 0);
                    _readBuffer = _readBuffer.Skip(4).ToArray();
                }

                if (_readBuffer.Length < _expectedLength)
                    return;

                var body = _readBuffer.Take(_expectedLength).ToArray();
                _readBuffer = _readBuffer.Skip(_expectedLength).ToArray();
                _expectedLength = -1;
                packetsProcessed++;

                try
                {
                    var packet = Common.Packet.Parser.ParseFrom(body);
                    int msgId = (int)packet.MsgId;
                    GD.Print($"[NetworkManager] Received Packet: msgId={msgId}, session={packet.Session}, dataLen={packet.Data.Length}");

                    // 缓存原始 payload 供场景读取
                    _lastMsgId = msgId;
                    _lastPayload = packet.Data.ToByteArray();

                    // 缓存已知响应
                    CacheResponse(msgId, packet.Data);

                    // 发射信号
                    EmitSignal(SignalName.PacketReceived, msgId);
                }
                catch (Exception e)
                {
                    GD.PushError($"[NetworkManager] Protobuf解析失败: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 自动缓存登录、选服等响应数据
        /// </summary>
        private void CacheResponse(int msgId, ByteString data)
        {
            try
            {
                switch ((MessageId)msgId)
                {
                    case MessageId.LoginAccountLoginRsp:
                        {
                            var rsp = Login.AccountLoginResponse.Parser.ParseFrom(data);
                            if (rsp.Code == Common.ErrorCode.Success)
                            {
                                AccountToken = rsp.AccountToken;
                                AccountId = rsp.AccountId;
                                LastServerId = rsp.LastServerId;
                                LastRoleName = rsp.LastRoleName;
                                Servers.Clear();
                                foreach (var s in rsp.Servers)
                                    Servers.Add(ServerInfoToDict(s));
                                GD.Print($"[NetworkManager] Login cached, accountId={AccountId}");
                            }
                        }
                        break;

                    case MessageId.LoginSelectServerRsp:
                        {
                            var rsp = Login.SelectServerResponse.Parser.ParseFrom(data);
                            if (rsp.Code == Common.ErrorCode.Success)
                            {
                                GatewayToken = rsp.GatewayToken;
                                MaxRoleCount = rsp.MaxRoleCount;
                                ServerTime = rsp.ServerTime;
                                Roles.Clear();
                                foreach (var r in rsp.Roles)
                                    Roles.Add(RoleBriefToDict(r));
                                GD.Print("[NetworkManager] SelectServer cached");
                            }
                        }
                        break;

                    case MessageId.GameEnterGameRsp:
                        {
                            var rsp = Game.EnterGameResponse.Parser.ParseFrom(data);
                            if (rsp.Code == Common.ErrorCode.Success && rsp.RoleInfo != null)
                            {
                                CachedRoleInfo = rsp.RoleInfo;
                                ServerTime = rsp.ServerTime;
                                GD.Print($"[NetworkManager] EnterGame cached, name={rsp.RoleInfo.RoleName} level={rsp.RoleInfo.Level}");
                                // 转发背包数据给 InventoryManager
                                var inv = GetTree()?.GetFirstNodeInGroup("inventory_manager");
                                if (inv is InventoryManager invObj && rsp.Items.Count > 0)
                                    invObj.UpdateFromProto(rsp.Items);
                            }
                        }
                        break;

                    case MessageId.GameCreateRoleRsp:
                        {
                            var rsp = Game.CreateRoleResponse.Parser.ParseFrom(data);
                            if (rsp.Code == Common.ErrorCode.Success && rsp.RoleInfo != null)
                            {
                                CachedRoleInfo = rsp.RoleInfo;
                                ServerTime = rsp.ServerTime;
                                GD.Print($"[NetworkManager] CreateRole cached, name={rsp.RoleInfo.RoleName}");
                                // 转发背包数据
                                var inv = GetTree()?.GetFirstNodeInGroup("inventory_manager");
                                if (inv is InventoryManager invObj && rsp.Items.Count > 0)
                                    invObj.UpdateFromProto(rsp.Items);
                                ServerTime = rsp.ServerTime;
                                GD.Print($"[NetworkManager] CreateRole cached, name={rsp.RoleInfo.RoleName}");
                            }
                        }
                        break;

                    case MessageId.GameMoveRsp:
                        {
                            var rsp = Game.MoveResponse.Parser.ParseFrom(data);
                            GD.Print($"[NetworkManager] MoveResponse: code={rsp.Code}, pos=({rsp.X},{rsp.Y})");
                            // 转发给 Player
                            var player = GetTree()?.GetFirstNodeInGroup("player");
                            if (player is Player playerObj)
                                playerObj.OnMoveResponse(rsp);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                GD.PushError($"[NetworkManager] CacheResponse failed: {e.Message}");
            }
        }

        private static Godot.Collections.Dictionary ServerInfoToDict(Server.ServerInfo s)
        {
            return new Godot.Collections.Dictionary
            {
                ["serverId"] = (int)s.ServerId,
                ["serverName"] = s.ServerName,
                ["status"] = (int)s.Status,
                ["onlineCount"] = (int)s.OnlineCount,
                ["isNew"] = s.IsNew,
                ["isRecommend"] = s.IsRecommend,
                ["hasRole"] = s.HasRole,
                ["roleCount"] = (int)s.RoleCount,
            };
        }

        private static Godot.Collections.Dictionary RoleBriefToDict(Login.RoleBrief r)
        {
            return new Godot.Collections.Dictionary
            {
                ["roleId"] = (long)r.RoleId,
                ["roleName"] = r.RoleName,
                ["level"] = (int)r.Level,
                ["avatarId"] = (int)r.AvatarId,
                ["lastLogin"] = (long)r.LastLogin,
                ["totalPower"] = (long)r.TotalPower,
            };
        }
    }
}
