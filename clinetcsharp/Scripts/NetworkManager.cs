using System;
using System.Collections.Generic;
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
        // ── Core events ──
        public event Action Connected;
        public event Action<string> ConnectionError;
        public event Action Disconnected;

        // ── Typed message events (proto objects) ──
        public event Action<Login.AccountLoginResponse> LoginResponse;
        public event Action<Login.SelectServerResponse> SelectServerResponse;
        public event Action<Game.EnterGameResponse> EnterGameResponse;
        public event Action<Game.CreateRoleResponse> CreateRoleResponse;
        public event Action<Game.MoveResponse> MoveResponse;
        public event Action<Game.MoveCancelNotify> MoveCancelNotify;
        public event Action<Game.MonsterMoveNotify> MonsterMoveNotify;
        public event Action<Game.MapInfoSyncNotify> MapInfoReceived;
        public event Action<Game.ChestUpdateNotify> ChestUpdateNotify;
        public event Action<Game.OpenChestResponse> OpenChestResponse;
        public event Action<Game.CombatLogNotify> CombatLogNotify;
        public event Action<Game.CombatStateNotify> CombatStateNotify;
        public event Action<Game.FullRoleInfo> RoleAttrUpdated;
        public event Action<Game.GmCommandResponse> GmResponse;
        public event Action<Game.UseItemResponse> UseItemResponse;
        public event Action<Game.DropItemResponse> DropItemResponse;

        public const string ServerHost = "127.0.0.1";
        public const int ServerPort = 8889;

        private StreamPeerTcp _tcp;
        private bool _connected = false;
        private byte[] _readBuffer = new byte[4096];
        private int _bufferOffset = 0;
        private int _bufferCount = 0;
        private int _expectedLength = -1;
        private uint _sessionCounter = 1;

        // ── 登录相关缓存 ──
        public string AccountToken { get; set; } = "";
        public uint AccountId { get; set; } = 0;
        public uint LastServerId { get; set; } = 0;
        public string LastRoleName { get; set; } = "";

        // ── 选服相关缓存 ──
        public string GatewayToken { get; set; } = "";
        public uint MaxRoleCount { get; set; } = 3;
        public uint ServerTime { get; set; } = 0;

        // ── 角色数据 ──
        public Game.FullRoleInfo CachedRoleInfo { get; set; } = null;
        public string CurrentMapName { get; set; } = "xinshoucun";
        public int SpawnGridX { get; set; } = 25;
        public int SpawnGridY { get; set; } = 25;

        // ── 列表缓存（proto 直接存储，不再转 Dict） ──
        public List<Server.ServerInfo> Servers { get; set; } = new();
        public List<Login.RoleBrief> Roles { get; set; } = new();
        public List<Game.ChestInfo> Chests { get; set; } = new();
        public List<Game.MonsterInfo> Monsters { get; set; } = new();
        public List<Game.ItemInfo> CachedItems { get; set; } = new();

        // ── 请求状态 ──
        public uint? PendingOpenChestId { get; set; } = null;

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
                    Disconnected?.Invoke();
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
            }
            else
            {
                GD.Print("[NetworkManager] Failed to connect, status: " + finalStatus);
                ConnectionError?.Invoke("无法连接到服务器");
            }
        }

        public void DisconnectFromServer()
        {
            if (_tcp != null)
                _tcp.DisconnectFromHost();
            _connected = false;
            _bufferOffset = 0;
            _bufferCount = 0;
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
                ConnectionError?.Invoke("发送消息失败: " + err);
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
                    // 确保 buffer 有足够空间
                    if (_bufferOffset + _bufferCount + chunkData.Length > _readBuffer.Length)
                    {
                        // 紧凑化：将未读数据移到 buffer 头部
                        if (_bufferCount + chunkData.Length <= _readBuffer.Length)
                        {
                            System.Array.Copy(_readBuffer, _bufferOffset, _readBuffer, 0, _bufferCount);
                            _bufferOffset = 0;
                        }
                        else
                        {
                            // 需要扩容
                            var newBuf = new byte[Math.Max(_readBuffer.Length * 2, _bufferCount + chunkData.Length)];
                            System.Array.Copy(_readBuffer, _bufferOffset, newBuf, 0, _bufferCount);
                            _readBuffer = newBuf;
                            _bufferOffset = 0;
                        }
                    }
                    chunkData.CopyTo(_readBuffer, _bufferOffset + _bufferCount);
                    _bufferCount += chunkData.Length;
                }
            }

            int maxPackets = 10;
            int packetsProcessed = 0;

            while (packetsProcessed < maxPackets)
            {
                if (_expectedLength < 0)
                {
                    if (_bufferCount < 4)
                        return;
                    // 直接从 buffer 读取 header，不分配新数组
                    if (!BitConverter.IsLittleEndian)
                    {
                        byte b0 = _readBuffer[_bufferOffset];
                        byte b1 = _readBuffer[_bufferOffset + 1];
                        byte b2 = _readBuffer[_bufferOffset + 2];
                        byte b3 = _readBuffer[_bufferOffset + 3];
                        _expectedLength = b3 << 24 | b2 << 16 | b1 << 8 | b0;
                    }
                    else
                    {
                        _expectedLength = BitConverter.ToInt32(_readBuffer, _bufferOffset);
                    }
                    _bufferOffset += 4;
                    _bufferCount -= 4;
                }

                if (_bufferCount < _expectedLength)
                    return;

                // 提取 body 数据
                var body = new byte[_expectedLength];
                System.Array.Copy(_readBuffer, _bufferOffset, body, 0, _expectedLength);
                _bufferOffset += _expectedLength;
                _bufferCount -= _expectedLength;
                _expectedLength = -1;
                packetsProcessed++;

                try
                {
                    var packet = Common.Packet.Parser.ParseFrom(body);
                    int msgId = (int)packet.MsgId;
                    GD.Print($"[NetworkManager] Received Packet: msgId={msgId}, session={packet.Session}, dataLen={packet.Data.Length}");

                    DispatchMessage(msgId, packet.Data);
                }
                catch (Exception e)
                {
                    GD.PushError($"[NetworkManager] Protobuf解析失败: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 解析 proto 并发射类型化事件
        /// </summary>
        private void DispatchMessage(int msgId, ByteString data)
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
                            Servers = new List<Server.ServerInfo>(rsp.Servers);
                            GD.Print($"[NetworkManager] Login cached, accountId={AccountId}");
                        }
                        LoginResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.LoginSelectServerRsp:
                    {
                        var rsp = Login.SelectServerResponse.Parser.ParseFrom(data);
                        if (rsp.Code == Common.ErrorCode.Success)
                        {
                            GatewayToken = rsp.GatewayToken;
                            MaxRoleCount = rsp.MaxRoleCount;
                            ServerTime = rsp.ServerTime;
                            Roles = new List<Login.RoleBrief>(rsp.Roles);
                            GD.Print("[NetworkManager] SelectServer cached");
                        }
                        SelectServerResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameEnterGameRsp:
                    {
                        var rsp = Game.EnterGameResponse.Parser.ParseFrom(data);
                        if (rsp.Code == Common.ErrorCode.Success && rsp.RoleInfo != null)
                        {
                            CachedRoleInfo = rsp.RoleInfo;
                            ServerTime = rsp.ServerTime;
                            CurrentMapName = rsp.RoleInfo.CurrentMap;
                            SpawnGridX = rsp.RoleInfo.GridX;
                            SpawnGridY = rsp.RoleInfo.GridY;
                            CachedItems = new List<Game.ItemInfo>(rsp.Items);
                            Chests = new List<Game.ChestInfo>(rsp.Chests);
                            GD.Print($"[NetworkManager] EnterGame cached, name={rsp.RoleInfo.RoleName} map={rsp.RoleInfo.CurrentMap} pos=({rsp.RoleInfo.GridX},{rsp.RoleInfo.GridY})");
                        }
                        EnterGameResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameCreateRoleRsp:
                    {
                        var rsp = Game.CreateRoleResponse.Parser.ParseFrom(data);
                        if (rsp.Code == Common.ErrorCode.Success && rsp.RoleInfo != null)
                        {
                            CachedRoleInfo = rsp.RoleInfo;
                            ServerTime = rsp.ServerTime;
                            CurrentMapName = rsp.RoleInfo.CurrentMap;
                            SpawnGridX = rsp.RoleInfo.GridX;
                            SpawnGridY = rsp.RoleInfo.GridY;
                            CachedItems = new List<Game.ItemInfo>(rsp.Items);
                            Chests = new List<Game.ChestInfo>(rsp.Chests);
                            GD.Print($"[NetworkManager] CreateRole cached, name={rsp.RoleInfo.RoleName}");
                        }
                        CreateRoleResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameMoveRsp:
                    {
                        var rsp = Game.MoveResponse.Parser.ParseFrom(data);
                        GD.Print($"[NetworkManager] MoveResponse: code={rsp.Code}, pos=({rsp.X},{rsp.Y})");
                        MoveResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameMoveCancelNotify:
                    {
                        var notify = Game.MoveCancelNotify.Parser.ParseFrom(data);
                        MoveCancelNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameMonsterMoveNotify:
                    {
                        var notify = Game.MonsterMoveNotify.Parser.ParseFrom(data);
                        MonsterMoveNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameMapInfoSyncNotify:
                    {
                        var notify = Game.MapInfoSyncNotify.Parser.ParseFrom(data);
                        CurrentMapName = notify.MapName;
                        Chests = new List<Game.ChestInfo>(notify.Chests);
                        Monsters = new List<Game.MonsterInfo>(notify.Monsters);
                        GD.Print($"[NetworkManager] MapInfoSyncNotify: map={notify.MapName}, chests={notify.Chests.Count}, monsters={notify.Monsters.Count}");
                        MapInfoReceived?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameChestUpdateNotify:
                    {
                        var notify = Game.ChestUpdateNotify.Parser.ParseFrom(data);
                        Chests.AddRange(notify.Chests);
                        GD.Print($"[NetworkManager] ChestUpdateNotify: +{notify.Chests.Count} chests");
                        ChestUpdateNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameOpenChestRsp:
                    {
                        var rsp = Game.OpenChestResponse.Parser.ParseFrom(data);
                        GD.Print($"[NetworkManager] OpenChest response: code={rsp.Code}, items={rsp.Items.Count}");
                        OpenChestResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameCombatLogNotify:
                    {
                        var notify = Game.CombatLogNotify.Parser.ParseFrom(data);
                        CombatLogNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameCombatStateNotify:
                    {
                        var notify = Game.CombatStateNotify.Parser.ParseFrom(data);
                        GD.Print($"[NetworkManager] CombatStateNotify: units={notify.Units.Count}");
                        CombatStateNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameRoleAttrNotify:
                    {
                        var roleInfo = Game.FullRoleInfo.Parser.ParseFrom(data);
                        CachedRoleInfo = roleInfo;
                        GD.Print($"[NetworkManager] RoleAttrNotify: attrs={roleInfo.Attrs.Count}");
                        RoleAttrUpdated?.Invoke(roleInfo);
                        break;
                    }

                    case MessageId.GameGmRsp:
                    {
                        var rsp = Game.GmCommandResponse.Parser.ParseFrom(data);
                        GD.Print($"[NetworkManager] GM response: code={rsp.Code}, msg={rsp.Message}");
                        GmResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameUseItemRsp:
                    {
                        var rsp = Game.UseItemResponse.Parser.ParseFrom(data);
                        GD.Print($"[NetworkManager] UseItem response: code={rsp.Code}");
                        UseItemResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameDropItemRsp:
                    {
                        var rsp = Game.DropItemResponse.Parser.ParseFrom(data);
                        GD.Print($"[NetworkManager] DropItem response: code={rsp.Code}");
                        DropItemResponse?.Invoke(rsp);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                GD.PushError($"[NetworkManager] DispatchMessage failed: {e.Message}");
            }
        }
    }
}
