using System;
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

        [Signal]
        public delegate void MapInfoReceivedEventHandler();

        [Signal]
        public delegate void MonsterMoveReceivedEventHandler(uint instanceId, int fromX, int fromY, int toX, int toY, string state, int durationMs);

        [Signal]
        public delegate void CombatLogReceivedEventHandler(Godot.Collections.Array entries);

        [Signal]
        public delegate void CombatStateReceivedEventHandler(Godot.Collections.Array units);

        [Signal]
        public delegate void MoveCancelReceivedEventHandler(ulong entityId, int rollbackX, int rollbackY);

        [Signal]
        public delegate void RoleAttrUpdatedEventHandler();

        public const string ServerHost = "127.0.0.1";
        public const int ServerPort = 8889;

        private StreamPeerTcp _tcp;
        private bool _connected = false;
        private byte[] _readBuffer = new byte[4096];
        private int _bufferOffset = 0;
        private int _bufferCount = 0;
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

        // 宝箱数据
        public Godot.Collections.Array Chests { get; set; } = new Godot.Collections.Array();
        public uint? PendingOpenChestId { get; set; } = null;

        // 怪物数据
        public Godot.Collections.Array Monsters { get; set; } = new Godot.Collections.Array();

        // 当前地图信息（从 FullRoleInfo 获取）
        public string CurrentMapName { get; set; } = "xinshoucun";
        public int SpawnGridX { get; set; } = 25;
        public int SpawnGridY { get; set; } = 25;

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
                                CurrentMapName = rsp.RoleInfo.CurrentMap;
                                SpawnGridX = rsp.RoleInfo.GridX;
                                SpawnGridY = rsp.RoleInfo.GridY;
                                GD.Print($"[NetworkManager] EnterGame cached, name={rsp.RoleInfo.RoleName} map={rsp.RoleInfo.CurrentMap} pos=({rsp.RoleInfo.GridX},{rsp.RoleInfo.GridY}) attrs={rsp.RoleInfo.Attrs.Count}");
                                // 同步角色属性到 Player
                                var enterPlayer = GetTree()?.GetFirstNodeInGroup("player") as Player;
                                enterPlayer?.ApplyRoleInfo(rsp.RoleInfo);
                                // 转发背包数据给 InventoryManager
                                var inv = GetTree()?.GetFirstNodeInGroup("inventory_manager");
                                if (inv is InventoryManager invObj && rsp.Items.Count > 0)
                                    invObj.UpdateFromProto(rsp.Items);
                                // 缓存宝箱数据（兼容旧逻辑，后续以 MapInfoSyncNotify 为准刷新）
                                Chests.Clear();
                                foreach (var c in rsp.Chests)
                                    Chests.Add(ChestInfoToDict(c));
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
                                CurrentMapName = rsp.RoleInfo.CurrentMap;
                                SpawnGridX = rsp.RoleInfo.GridX;
                                SpawnGridY = rsp.RoleInfo.GridY;
                                GD.Print($"[NetworkManager] CreateRole cached, name={rsp.RoleInfo.RoleName} map={rsp.RoleInfo.CurrentMap} attrs={rsp.RoleInfo.Attrs.Count}");
                                // 同步角色属性到 Player
                                var createPlayer = GetTree()?.GetFirstNodeInGroup("player") as Player;
                                createPlayer?.ApplyRoleInfo(rsp.RoleInfo);
                                // 转发背包数据
                                var inv = GetTree()?.GetFirstNodeInGroup("inventory_manager");
                                if (inv is InventoryManager invObj && rsp.Items.Count > 0)
                                    invObj.UpdateFromProto(rsp.Items);
                                // 缓存宝箱数据
                                Chests.Clear();
                                foreach (var c in rsp.Chests)
                                    Chests.Add(ChestInfoToDict(c));
                            }
                        }
                        break;

                    case MessageId.GameMoveRsp:
                        {
                            var rsp = Game.MoveResponse.Parser.ParseFrom(data);
                            GD.Print($"[NetworkManager] MoveResponse: code={rsp.Code}, pos=({rsp.X},{rsp.Y})");
                            var player = GetTree()?.GetFirstNodeInGroup("player");
                            if (player is Player playerObj)
                                playerObj.OnMoveResponse(rsp);
                        }
                        break;

                    case MessageId.GameGmRsp:
                        {
                            var rsp = Game.GmCommandResponse.Parser.ParseFrom(data);
                            GD.Print($"[NetworkManager] GM response: code={rsp.Code}, msg={rsp.Message}");
                            // 查找 GMPanel（在 Main 场景下）
                            foreach (var child in GetTree().Root.GetChildren())
                            {
                                var gm = child.GetNodeOrNull<GMPanel>("GMPanel");
                                if (gm != null) { gm.OnGmResponse(rsp); break; }
                            }
                        }
                        break;

                    case MessageId.GameOpenChestRsp:
                        {
                            var rsp = Game.OpenChestResponse.Parser.ParseFrom(data);
                            GD.Print($"[NetworkManager] OpenChest response: code={rsp.Code}, items={rsp.Items.Count}");
                            var chestMgr = GetTree()?.GetFirstNodeInGroup("chest_manager");
                            if (chestMgr is ChestManager cm3 && PendingOpenChestId.HasValue)
                            {
                                cm3.OnOpenChestResponse(rsp, PendingOpenChestId.Value);
                                PendingOpenChestId = null;
                            }
                        }
                        break;

                    case MessageId.GameChestUpdateNotify:
                        {
                            var notify = Game.ChestUpdateNotify.Parser.ParseFrom(data);
                            GD.Print($"[NetworkManager] ChestUpdateNotify received, count={notify.Chests.Count}");
                            foreach (var c in notify.Chests)
                                Chests.Add(ChestInfoToDict(c));
                            var chestMgr = GetTree()?.GetFirstNodeInGroup("chest_manager");
                            if (chestMgr is ChestManager cm)
                                cm.SpawnChests(Chests, 111);
                        }
                        break;

                    case MessageId.GameMapInfoSyncNotify:
                        {
                            var notify = Game.MapInfoSyncNotify.Parser.ParseFrom(data);
                            GD.Print($"[NetworkManager] MapInfoSyncNotify received: map={notify.MapName}, chests={notify.Chests.Count}, monsters={notify.Monsters.Count}");
                            CurrentMapName = notify.MapName;
                            Chests.Clear();
                            foreach (var c in notify.Chests)
                                Chests.Add(ChestInfoToDict(c));
                            Monsters.Clear();
                            foreach (var m in notify.Monsters)
                                Monsters.Add(MonsterInfoToDict(m));
                            EmitSignal(SignalName.MapInfoReceived);
                        }
                        break;

                    case MessageId.GameMoveCancelNotify:
                        {
                            var notify = Game.MoveCancelNotify.Parser.ParseFrom(data);
                            EmitSignal(SignalName.MoveCancelReceived, notify.EntityId, notify.RollbackX, notify.RollbackY);
                        }
                        break;

                    case MessageId.GameMonsterMoveNotify:
                        {
                            var notify = Game.MonsterMoveNotify.Parser.ParseFrom(data);
                            EmitSignal(SignalName.MonsterMoveReceived, notify.InstanceId, notify.FromX, notify.FromY, notify.ToX, notify.ToY, notify.State, notify.DurationMs);
                        }
                        break;

                    case MessageId.GameCombatLogNotify:
                        {
                            var notify = Game.CombatLogNotify.Parser.ParseFrom(data);
                            var arr = new Godot.Collections.Array();
                            foreach (var e in notify.Entries)
                            {
                                var dict = new Godot.Collections.Dictionary
                                {
                                    ["log_type"] = (int)e.LogType,
                                    ["timestamp"] = (long)e.Timestamp,
                                    ["actor_name"] = e.ActorName,
                                    ["target_name"] = e.TargetName,
                                    ["skill_name"] = e.SkillName,
                                    ["value"] = e.Value,
                                    ["extra"] = e.Extra,
                                };
                                arr.Add(dict);
                            }
                            EmitSignal(SignalName.CombatLogReceived, arr);
                        }
                        break;

                    case MessageId.GameCombatStateNotify:
                        {
                            var notify = Game.CombatStateNotify.Parser.ParseFrom(data);
                            var arr = new Godot.Collections.Array();
                            foreach (var u in notify.Units)
                            {
                                var dict = new Godot.Collections.Dictionary
                                {
                                    ["entity_id"] = (long)u.EntityId,
                                    ["entity_name"] = u.EntityName,
                                    ["atb"] = u.Atb,
                                    ["is_player"] = u.IsPlayer,
                                };
                                arr.Add(dict);
                            }
                            GD.Print($"[NetworkManager] CombatStateNotify received, units={arr.Count}");
                            EmitSignal(SignalName.CombatStateReceived, arr);
                        }
                        break;

                    case MessageId.GameRoleAttrNotify:
                        {
                            var roleInfo = Game.FullRoleInfo.Parser.ParseFrom(data);
                            CachedRoleInfo = roleInfo;
                            GD.Print($"[NetworkManager] RoleAttrNotify received, attrs={roleInfo.Attrs.Count}");

                            // 自动同步到 Player 节点
                            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
                            player?.ApplyRoleInfo(roleInfo);

                            EmitSignal(SignalName.RoleAttrUpdated);
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

        private static Godot.Collections.Dictionary ChestInfoToDict(Game.ChestInfo c)
        {
            return new Godot.Collections.Dictionary
            {
                ["chest_id"] = (int)c.ChestId,
                ["x"] = c.X,
                ["y"] = c.Y,
                ["opened"] = c.Opened,
            };
        }

        private static Godot.Collections.Dictionary MonsterInfoToDict(Game.MonsterInfo m)
        {
            var attrs = new Godot.Collections.Array();
            foreach (var attr in m.Attrs)
            {
                attrs.Add(new Godot.Collections.Dictionary
                {
                    ["attr_key"] = (int)attr.AttrKey,
                    ["attr_value"] = attr.AttrValue,
                });
            }

            return new Godot.Collections.Dictionary
            {
                ["instance_id"] = (int)m.InstanceId,
                ["monster_id"] = (int)m.MonsterId,
                ["x"] = m.X,
                ["y"] = m.Y,
                ["name"] = m.Name,
                ["level"] = (int)m.Level,
                ["attrs"] = attrs,
            };
        }
    }
}
