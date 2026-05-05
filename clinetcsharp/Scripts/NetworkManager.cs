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
        public event Action<string> Kicked;          // 被服务器踢下线（含原因）
        public event Action<Game.ChangeMapResponse> ChangeMapResponse;

        // ── Typed message events (proto objects) ──
        public event Action<Login.AccountLoginResponse> LoginResponse;
        public event Action<Login.SelectServerResponse> SelectServerResponse;
        public event Action<Game.EnterGameResponse> EnterGameResponse;
        public event Action<Game.CreateRoleResponse> CreateRoleResponse;
        public event Action<Game.MoveResponse> MoveResponse;
        public event Action<Game.MoveCancelNotify> MoveCancelNotify;
        public event Action<Game.MonsterMoveNotify> MonsterMoveNotify;
        public event Action<Game.MonsterMoveCancelNotify> MonsterMoveCancelNotify;
        public event Action<Game.MapInfoSyncNotify> MapInfoReceived;
        public event Action<Game.ChestUpdateNotify> ChestUpdateNotify;
        public event Action<Game.DropSpawnNotify> DropSpawnNotify;
        public event Action<Game.DropPickupNotify> DropPickupNotify;
        public event Action<Game.DropRemoveNotify> DropRemoveNotify;
        public event Action<Game.OpenChestResponse> OpenChestResponse;
        public event Action<Game.CombatLogNotify> CombatLogNotify;
        public event Action<Game.CombatStateNotify> CombatStateNotify;
        public event Action<Game.BuffUpdateNotify> BuffUpdateNotify;
        public event Action<Game.CombatStartNotify> CombatStartNotify;
        public event Action<Game.CombatEndNotify> CombatEndNotify;
        public event Action<Game.FullRoleInfo> RoleAttrUpdated;
        public event Action<Game.GmCommandResponse> GmResponse;
        public event Action<Game.UseItemResponse> UseItemResponse;
        public event Action<Game.DropItemResponse> DropItemResponse;
        public event Action<Game.PlayerDeathNotify> PlayerDeathNotify;
        public event Action<Game.EquipSkillResponse> EquipSkillResponse;
        public event Action<Game.UnequipSkillResponse> UnequipSkillResponse;
        public event Action<Game.NpcInteractNotify> NpcInteractNotify;
        public event Action<Game.NpcCombatResponse> NpcCombatResponse;
        public event Action<Game.ChangeJobResponse> ChangeJobResponse;
        public event Action<Game.LevelUpNotify> LevelUpNotify;

        public const string ServerHost = "127.0.0.1";
        public const int ServerPort = 8889;

        private StreamPeerTcp _tcp;
        private bool _connected = false;
        private byte[] _readBuffer = new byte[8192];
        private int _bufferOffset = 0;
        private int _bufferCount = 0;
        private int _expectedLength = -1;
        private uint _sessionCounter = 1;

        // ── 心跳 ──
        private double _heartbeatInterval = 30.0;   // 秒
        private double _heartbeatTimer = 0.0;

        // ── 帧级缓存时间戳（避免每次 SendPacket 都系统调用） ──
        private ulong _cachedTimestamp = 0;
        private bool _timestampDirty = true;

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
        public List<Game.NpcInfo> Npcs { get; set; } = new();
        public List<Game.DropItemInfo> Drops { get; set; } = new();
        public List<Game.ItemInfo> CachedItems { get; set; } = new();
        public List<uint> CachedLearnedSkills { get; set; } = new();
        public List<uint> CachedEquippedSkills { get; set; } = new();

        // ── 请求状态 ──
        public uint? PendingOpenChestId { get; set; } = null;

        public override void _Ready()
        {
            SkillDataUtil.Load();
            GD.Print("[NetworkManager] _ready() initializing...");
            _tcp = new StreamPeerTcp();
            GD.Print("[NetworkManager] Initialized");
        }

        public override void _Process(double _delta)
        {
            // 帧首标记时间戳需要刷新
            _timestampDirty = true;

            if (_tcp == null)
                return;

            var status = _tcp.GetStatus();
            if (status == StreamPeerTcp.Status.Connected)
            {
                _tcp.Poll();
                ReadPackets();

                // 心跳
                if (_connected)
                {
                    _heartbeatTimer += _delta;
                    if (_heartbeatTimer >= _heartbeatInterval)
                    {
                        _heartbeatTimer = 0.0;
                        SendHeartbeat();
                    }
                }
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
            _heartbeatTimer = 0.0;
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
            if (!IsServerConnected())
            {
                GD.PushError("[NetworkManager] 未连接到服务器，无法发送消息");
                return false;
            }

            // 帧级缓存时间戳，避免每次 SendPacket 都系统调用
            if (_timestampDirty)
            {
                _cachedTimestamp = (ulong)Time.GetUnixTimeFromSystem();
                _timestampDirty = false;
            }

            var packet = new Common.Packet
            {
                MsgId = (uint)msgId,
                Session = _sessionCounter++,
                Data = ByteString.CopyFrom(data.ToByteArray()),
                Timestamp = _cachedTimestamp
            };

            var body = packet.ToByteArray();
            var header = BitConverter.GetBytes((uint)body.Length);
            if (!BitConverter.IsLittleEndian)
                System.Array.Reverse(header);

            var combined = new byte[header.Length + body.Length];
            header.CopyTo(combined, 0);
            body.CopyTo(combined, header.Length);

            var err = _tcp.PutData(combined);
            if (err != Error.Ok)
            {
                GD.PushError($"[NetworkManager] PutData failed: {err}");
                ConnectionError?.Invoke("发送消息失败: " + err);
                return false;
            }
            return true;
        }

        private void SendHeartbeat()
        {
            if (!IsServerConnected()) return;
            var req = new Gateway.HeartbeatRequest
            {
                ClientTime = (ulong)Time.GetUnixTimeFromSystem()
            };
            SendPacket(MessageId.GatewayHeartbeatReq, req);
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

                // 提取 body：直接从 buffer 创建 ByteString，省掉中间 byte[]
                var body = ByteString.CopyFrom(_readBuffer, _bufferOffset, _expectedLength);
                _bufferOffset += _expectedLength;
                _bufferCount -= _expectedLength;
                _expectedLength = -1;
                packetsProcessed++;

                try
                {
                    var packet = Common.Packet.Parser.ParseFrom(body);
                    int msgId = (int)packet.MsgId;

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
                    // ── Gateway ──
                    case MessageId.GatewayHeartbeatRsp:
                    {
                        var rsp = Gateway.HeartbeatResponse.Parser.ParseFrom(data);
                        ServerTime = (uint)rsp.ServerTime;
                        break;
                    }

                    case MessageId.GatewayKickNotify:
                    {
                        var notify = Gateway.DisconnectNotify.Parser.ParseFrom(data);
                        GD.Print($"[NetworkManager] Kicked by server: reason={notify.Reason}");
                        _connected = false;
                        Kicked?.Invoke(notify.Reason);
                        break;
                    }

                    case MessageId.GatewayDisconnectNotify:
                    {
                        var notify = Gateway.DisconnectNotify.Parser.ParseFrom(data);
                        GD.Print($"[NetworkManager] Server disconnect: reason={notify.Reason}");
                        _connected = false;
                        Disconnected?.Invoke();
                        break;
                    }

                    // ── Login ──
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
                            ServerTime = (uint)rsp.ServerTime;
                            Roles = new List<Login.RoleBrief>(rsp.Roles);
                        }
                        SelectServerResponse?.Invoke(rsp);
                        break;
                    }

                    // ── Game entry (shared cache logic) ──
                    case MessageId.GameEnterGameRsp:
                    {
                        var rsp = Game.EnterGameResponse.Parser.ParseFrom(data);
                        if (rsp.Code == Common.ErrorCode.Success && rsp.RoleInfo != null)
                            CacheRoleAndMapData(rsp.RoleInfo, rsp.Items, rsp.Chests, rsp.ServerTime);
                        EnterGameResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameCreateRoleRsp:
                    {
                        var rsp = Game.CreateRoleResponse.Parser.ParseFrom(data);
                        if (rsp.Code == Common.ErrorCode.Success && rsp.RoleInfo != null)
                            CacheRoleAndMapData(rsp.RoleInfo, rsp.Items, rsp.Chests, rsp.ServerTime);
                        CreateRoleResponse?.Invoke(rsp);
                        break;
                    }

                    // ── Map ──
                    case MessageId.GameMapInfoSyncNotify:
                    {
                        var notify = Game.MapInfoSyncNotify.Parser.ParseFrom(data);
                        CurrentMapName = notify.MapName;
                        Chests = new List<Game.ChestInfo>(notify.Chests);
                        Monsters = new List<Game.MonsterInfo>(notify.Monsters);
                        Npcs = new List<Game.NpcInfo>(notify.Npcs);
                        Drops = new List<Game.DropItemInfo>(notify.Drops);
                        GD.Print($"[NetworkManager] MapInfoSync map={notify.MapName} chests={Chests.Count} monsters={Monsters.Count} npcs={Npcs.Count} drops={Drops.Count}");
                        MapInfoReceived?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameChangeMapRsp:
                    {
                        var rsp = Game.ChangeMapResponse.Parser.ParseFrom(data);
                        if (rsp.Code == Common.ErrorCode.Success)
                        {
                            CurrentMapName = rsp.MapName;
                            SpawnGridX = (int)rsp.SpawnX;
                            SpawnGridY = (int)rsp.SpawnY;
                        }
                        ChangeMapResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameChestUpdateNotify:
                    {
                        var notify = Game.ChestUpdateNotify.Parser.ParseFrom(data);
                        Chests = new List<Game.ChestInfo>(Chests);
                        Chests.AddRange(notify.Chests);
                        ChestUpdateNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameDropSpawnNotify:
                    {
                        var notify = Game.DropSpawnNotify.Parser.ParseFrom(data);
                        DropSpawnNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameDropPickupNotify:
                    {
                        var notify = Game.DropPickupNotify.Parser.ParseFrom(data);
                        DropPickupNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameDropRemoveNotify:
                    {
                        var notify = Game.DropRemoveNotify.Parser.ParseFrom(data);
                        DropRemoveNotify?.Invoke(notify);
                        break;
                    }

                    // ── Movement ──
                    case MessageId.GameMoveRsp:
                    {
                        var rsp = Game.MoveResponse.Parser.ParseFrom(data);
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

                    case MessageId.GameMonsterMoveCancelNotify:
                    {
                        var notify = Game.MonsterMoveCancelNotify.Parser.ParseFrom(data);
                        MonsterMoveCancelNotify?.Invoke(notify);
                        break;
                    }

                    // ── Combat ──
                    case MessageId.GameCombatLogNotify:
                    {
                        var notify = Game.CombatLogNotify.Parser.ParseFrom(data);
                        CombatLogNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameCombatStateNotify:
                    {
                        var notify = Game.CombatStateNotify.Parser.ParseFrom(data);
                        CombatStateNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameBuffUpdateNotify:
                    {
                        var notify = Game.BuffUpdateNotify.Parser.ParseFrom(data);
                        BuffUpdateNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameCombatStartNotify:
                    {
                        var notify = Game.CombatStartNotify.Parser.ParseFrom(data);
                        CombatStartNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameCombatEndNotify:
                    {
                        var notify = Game.CombatEndNotify.Parser.ParseFrom(data);
                        CombatEndNotify?.Invoke(notify);
                        break;
                    }

                    // ── Role ──
                    case MessageId.GameRoleAttrNotify:
                    {
                        var roleInfo = Game.FullRoleInfo.Parser.ParseFrom(data);
                        CachedRoleInfo = roleInfo;
                        CachedLearnedSkills = new List<uint>(roleInfo.LearnedSkills);
                        CachedEquippedSkills = new List<uint>(roleInfo.EquippedSkills);
                        RoleAttrUpdated?.Invoke(roleInfo);
                        break;
                    }

                    // ── Items ──
                    case MessageId.GameOpenChestRsp:
                    {
                        var rsp = Game.OpenChestResponse.Parser.ParseFrom(data);
                        OpenChestResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameUseItemRsp:
                    {
                        var rsp = Game.UseItemResponse.Parser.ParseFrom(data);
                        UseItemResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameDropItemRsp:
                    {
                        var rsp = Game.DropItemResponse.Parser.ParseFrom(data);
                        DropItemResponse?.Invoke(rsp);
                        break;
                    }

                    // ── GM ──
                    case MessageId.GameGmRsp:
                    {
                        var rsp = Game.GmCommandResponse.Parser.ParseFrom(data);
                        // learnskill 命令会返回更新后的技能列表，同步缓存
                        if (rsp.LearnedSkills.Count > 0)
                            CachedLearnedSkills = new List<uint>(rsp.LearnedSkills);
                        if (rsp.EquippedSkills.Count > 0)
                            CachedEquippedSkills = new List<uint>(rsp.EquippedSkills);
                        GmResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GamePlayerDeathNotify:
                    {
                        var notify = Game.PlayerDeathNotify.Parser.ParseFrom(data);
                        PlayerDeathNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameEquipSkillRsp:
                    {
                        var rsp = Game.EquipSkillResponse.Parser.ParseFrom(data);
                        EquipSkillResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameUnequipSkillRsp:
                    {
                        var rsp = Game.UnequipSkillResponse.Parser.ParseFrom(data);
                        UnequipSkillResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameSetPreferredSkillRsp:
                    {
                        var rsp = Game.SetPreferredSkillResponse.Parser.ParseFrom(data);
                        GD.Print($"[Network] SetPreferredSkill: skill={rsp.PreferredSkillId} code={rsp.Code}");
                        break;
                    }

                    case MessageId.GameNpcInteractNotify:
                    {
                        var notify = Game.NpcInteractNotify.Parser.ParseFrom(data);
                        NpcInteractNotify?.Invoke(notify);
                        break;
                    }

                    case MessageId.GameNpcCombatRsp:
                    {
                        var rsp = Game.NpcCombatResponse.Parser.ParseFrom(data);
                        NpcCombatResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameChangeJobRsp:
                    {
                        var rsp = Game.ChangeJobResponse.Parser.ParseFrom(data);
                        if (rsp.Code == Common.ErrorCode.Success)
                        {
                            CachedLearnedSkills = new List<uint>(rsp.LearnedSkills);
                            CachedEquippedSkills = new List<uint>(rsp.EquippedSkills);
                        }
                        ChangeJobResponse?.Invoke(rsp);
                        break;
                    }

                    case MessageId.GameLevelUpNotify:
                    {
                        var notify = Game.LevelUpNotify.Parser.ParseFrom(data);
                        LevelUpNotify?.Invoke(notify);
                        break;
                    }

                    default:
                        GD.Print($"[NetworkManager] Unhandled msgId={msgId}");
                        break;
                }
            }
            catch (Exception e)
            {
                GD.PushError($"[NetworkManager] DispatchMessage failed for msgId={msgId}: {e.Message}");
            }
        }

        /// <summary>
        /// 统一缓存角色/背包/宝箱数据（EnterGame 和 CreateRole 共用）
        /// </summary>
        private void CacheRoleAndMapData(
            Game.FullRoleInfo roleInfo,
            Google.Protobuf.Collections.RepeatedField<Game.ItemInfo> items,
            Google.Protobuf.Collections.RepeatedField<Game.ChestInfo> chests,
            uint serverTime)
        {
            CachedRoleInfo = roleInfo;
            ServerTime = serverTime;
            CurrentMapName = roleInfo.CurrentMap;
            SpawnGridX = roleInfo.GridX;
            SpawnGridY = roleInfo.GridY;
            CachedItems = new List<Game.ItemInfo>(items);
            Chests = new List<Game.ChestInfo>(chests);
            CachedLearnedSkills = new List<uint>(roleInfo.LearnedSkills);
            CachedEquippedSkills = new List<uint>(roleInfo.EquippedSkills);
            GD.Print($"[NetworkManager] Cached role={roleInfo.RoleName} map={roleInfo.CurrentMap} pos=({roleInfo.GridX},{roleInfo.GridY})");
        }
    }
}
