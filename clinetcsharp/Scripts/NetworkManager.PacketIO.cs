using System;
using Google.Protobuf;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
        // 按 session 关联的一次性响应回调（当前用于截屏请求服务器日志路径）
        private readonly System.Collections.Generic.Dictionary<uint, System.Action<string>> _pendingLogPathCallbacks = new();

        public bool SendPacket(MessageId msgId, IMessage data)
        {
            return SendPacket(msgId, data, _sessionCounter++);
        }

        /// <summary>
        /// 使用外部指定的 session 发送消息（用于需要按 session 关联响应回调的场景）。
        /// </summary>
        public bool SendPacket(MessageId msgId, IMessage data, uint session)
        {
            if (!IsServerConnected())
            {
                GD.PushError("[NetworkManager] 未连接到服务器，无法发送消息");
                return false;
            }

            if (_timestampDirty)
            {
                _cachedTimestamp = (ulong)Time.GetUnixTimeFromSystem();
                _timestampDirty = false;
            }

            var packet = new Common.Packet
            {
                MsgId = (uint)msgId,
                Session = session,
                Data = data.ToByteString(),
                Timestamp = _cachedTimestamp,
            };

            var body = packet.ToByteArray();
            var header = BitConverter.GetBytes((uint)body.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(header);

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

        private void ReadPackets()
        {
            int available;
            try
            {
                available = _tcp.GetAvailableBytes();
            }
            catch (Exception ex)
            {
                GD.PushError($"[NetworkManager] GetAvailableBytes failed: {ex.Message}");
                return;
            }

            if (available > 0)
            {
                var chunk = _tcp.GetPartialData(available);
                if (chunk[0].AsInt32() == (int)Error.Ok)
                {
                    var chunkData = chunk[1].AsByteArray();
                    EnsureBufferCapacity(chunkData.Length);
                    chunkData.CopyTo(_readBuffer, _bufferOffset + _bufferCount);
                    _bufferCount += chunkData.Length;
                }
            }

            int maxPackets = 50;
            int packetsProcessed = 0;
            while (packetsProcessed < maxPackets)
            {
                if (!TryReadExpectedLength())
                    return;

                if (_bufferCount < _expectedLength)
                    return;

                // 直接从 _readBuffer 解析，跳过 ByteString.CopyFrom 的中间拷贝
                int bodyOffset = _bufferOffset;
                int bodyLength = _expectedLength;
                _bufferOffset += _expectedLength;
                _bufferCount -= _expectedLength;
                _expectedLength = -1;
                packetsProcessed++;

                try
                {
                    var packet = Common.Packet.Parser.ParseFrom(_readBuffer, bodyOffset, bodyLength);
                    DispatchMessage((int)packet.MsgId, packet.Data, packet.Session);
                }
                catch (Exception e)
                {
                    GD.PushError($"[NetworkManager] Protobuf解析失败: {e.Message}");
                }
            }
        }

        private void EnsureBufferCapacity(int incomingLength)
        {
            if (_bufferOffset + _bufferCount + incomingLength <= _readBuffer.Length)
                return;

            if (_bufferCount + incomingLength <= _readBuffer.Length)
            {
                Array.Copy(_readBuffer, _bufferOffset, _readBuffer, 0, _bufferCount);
                _bufferOffset = 0;
                return;
            }

            var newBuffer = new byte[Math.Max(_readBuffer.Length * 2, _bufferCount + incomingLength)];
            Array.Copy(_readBuffer, _bufferOffset, newBuffer, 0, _bufferCount);
            _readBuffer = newBuffer;
            _bufferOffset = 0;
        }

        private bool TryReadExpectedLength()
        {
            if (_expectedLength >= 0)
                return true;

            if (_bufferCount < 4)
                return false;

            if (!BitConverter.IsLittleEndian)
            {
                byte b0 = _readBuffer[_bufferOffset];
                byte b1 = _readBuffer[_bufferOffset + 1];
                byte b2 = _readBuffer[_bufferOffset + 2];
                byte b3 = _readBuffer[_bufferOffset + 3];
                _expectedLength = (b3 << 24) | (b2 << 16) | (b1 << 8) | b0;
            }
            else
            {
                _expectedLength = BitConverter.ToInt32(_readBuffer, _bufferOffset);
            }

            _bufferOffset += 4;
            _bufferCount -= 4;
            return true;
        }

        /// <summary>
        /// 请求服务器当前正在写入的日志文件绝对路径。
        /// 收到响应后调用 onResult(logPath)。未连接服务器时静默跳过。
        /// </summary>
        public void RequestServerLogPath(System.Action<string> onResult)
        {
            if (!IsServerConnected())
                return;

            uint session = _sessionCounter++;
            _pendingLogPathCallbacks[session] = onResult;
            SendPacket(MessageId.GameGetServerLogPathReq, new Game.GetServerLogPathRequest(), session);
        }

        /// <summary>
        /// 断线时清理挂起的一次性回调，避免残留。
        /// </summary>
        private void ClearPendingCallbacks()
        {
            _pendingLogPathCallbacks.Clear();
        }
    }
}
