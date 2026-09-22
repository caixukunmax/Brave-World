using System;
using System.Collections;
using Google.Protobuf;
using Protocol;
using UnityEngine;

namespace UnityClientSharp.Net
{
    /// <summary>
    /// 连接生命周期 / 封包收发 / 心跳 — 移植自 Godot NetworkManager.Connection + PacketIO。
    /// </summary>
    public partial class NetworkManager
    {
        private void Update()
        {
            _timestampDirty = true;

            if (_tcp == null || !_connected)
                return;

            ReadPackets();

            _heartbeatTimer += Time.unscaledDeltaTime;
            if (_heartbeatTimer >= HeartbeatInterval)
            {
                _heartbeatTimer = 0f;
                SendHeartbeat();
            }

            // 被动断线检测（对齐 Godot 每帧查 GetStatus）
            if (_tcp.PollDisconnected())
            {
                _connected = false;
                ClearPendingCallbacks();
                Disconnected?.Invoke();
            }
        }

        public void ConnectToServer() => StartCoroutine(ConnectRoutine());

        /// <summary>对齐 Godot ConnectToServer：清理旧连接 → 异步连接 → 50ms×100 轮询等待。</summary>
        private IEnumerator ConnectRoutine()
        {
            Debug.Log($"[NetworkManager] Connecting to {ServerHost}:{ServerPort}");

            if (_connected)
            {
                Debug.Log("[NetworkManager] Already connected");
                yield break;
            }

            _tcp?.Close();
            _tcp = new TcpTransport();

            if (!_tcp.BeginConnect(ServerHost, ServerPort, out var beginError))
            {
                Debug.Log("[NetworkManager] Connection failed: " + beginError);
                ConnectionError?.Invoke("连接服务器失败: " + beginError);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 5f; // 100 × 50ms
            while (!_tcp.ConnectCompleted && Time.realtimeSinceStartup < deadline)
                yield return new WaitForSecondsRealtime(0.05f);

            bool ok = false;
            string finishError = null;
            if (_tcp.ConnectCompleted)
                ok = _tcp.FinishConnect(out finishError);

            if (ok)
            {
                _connected = true;
                Debug.Log("[NetworkManager] Connected successfully");
                Connected?.Invoke();
                yield break;
            }

            Debug.Log("[NetworkManager] Failed to connect: " + (finishError ?? "timeout"));
            ConnectionError?.Invoke("无法连接到服务器");
        }

        public void DisconnectFromServer()
        {
            _tcp?.Close();
            _connected = false;
            _heartbeatTimer = 0f;
            _framer.Reset();
            ClearPendingCallbacks();
        }

        public bool IsServerConnected() => _connected && _tcp != null && _tcp.IsConnected && !_tcp.PollDisconnected();

        public bool SendPacket(MessageId msgId, IMessage data)
        {
            return SendPacket(msgId, data, _sessionCounter++);
        }

        /// <summary>使用外部指定的 session 发送（用于按 session 关联响应回调的场景）。</summary>
        public bool SendPacket(MessageId msgId, IMessage data, uint session)
        {
            if (!IsServerConnected())
            {
                Debug.LogError("[NetworkManager] 未连接到服务器，无法发送消息");
                return false;
            }

            if (_timestampDirty)
            {
                _cachedTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _timestampDirty = false;
            }

            var packet = new Common.Packet
            {
                MsgId = (uint)msgId,
                Session = session,
                Data = data.ToByteString(),
                Timestamp = _cachedTimestamp,
            };

            // 帧格式：[4字节小端 uint32 body长度][Common.Packet protobuf]
            var body = packet.ToByteArray();
            var combined = new byte[4 + body.Length];
            combined[0] = (byte)(body.Length & 0xFF);
            combined[1] = (byte)((body.Length >> 8) & 0xFF);
            combined[2] = (byte)((body.Length >> 16) & 0xFF);
            combined[3] = (byte)((body.Length >> 24) & 0xFF);
            Array.Copy(body, 0, combined, 4, body.Length);

            if (!_tcp.Send(combined))
            {
                Debug.LogError("[NetworkManager] Send failed");
                ConnectionError?.Invoke("发送消息失败");
                return false;
            }
            return true;
        }

        private void ReadPackets()
        {
            // 接收：非阻塞读入 framer
            var chunk = new byte[16384];
            int n;
            while ((n = _tcp.Receive(chunk, 0, chunk.Length)) > 0)
                _framer.Write(chunk, n);
            if (n < 0) return; // 连接异常，交由 PollDisconnected 处理

            // 每帧最多处理 50 包（对齐 Godot）
            const int maxPackets = 50;
            for (int i = 0; i < maxPackets; i++)
            {
                if (!_framer.TryReadFrame(out int frameOffset, out int frameLength))
                    break;
                try
                {
                    var packet = Common.Packet.Parser.ParseFrom(_framer.Buffer, frameOffset, frameLength);
                    DispatchMessage((int)packet.MsgId, packet.Data, packet.Session);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkManager] Protobuf解析失败: {e.Message}");
                }
            }
        }

        private void SendHeartbeat()
        {
            if (!IsServerConnected())
                return;

            var req = new Gateway.HeartbeatRequest
            {
                ClientTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            SendPacket(MessageId.GatewayHeartbeatReq, req);
        }

        /// <summary>请求服务器当前日志文件路径（session 关联一次性回调）。</summary>
        public void RequestServerLogPath(Action<string> onResult)
        {
            if (!IsServerConnected())
                return;

            uint session = _sessionCounter++;
            _pendingLogPathCallbacks[session] = onResult;
            SendPacket(MessageId.GameGetServerLogPathReq, new Game.GetServerLogPathRequest(), session);
        }

        private void ClearPendingCallbacks()
        {
            _pendingLogPathCallbacks.Clear();
        }
    }
}
