using System;

namespace UnityClientSharp.Net
{
    /// <summary>
    /// 封包帧拆解器 — 移植自 Godot NetworkManager.PacketIO 的接收缓冲逻辑：
    /// [4字节小端 uint32 长度][Common.Packet body]，8KB 起始缓冲，compact/翻倍扩容。
    /// 纯逻辑、与传输解耦，供单元测试直接驱动。
    /// </summary>
    public class PacketFramer
    {
        private byte[] _buffer = new byte[8192];
        private int _offset;
        private int _count;
        private int _expectedLength = -1;

        /// <summary>当前缓冲（解析帧时直接从此数组读，零中间拷贝）。</summary>
        public byte[] Buffer => _buffer;

        public void Reset()
        {
            _offset = 0;
            _count = 0;
            _expectedLength = -1;
        }

        /// <summary>追加收到的字节块。</summary>
        public void Write(byte[] data, int length)
        {
            EnsureCapacity(length);
            Array.Copy(data, 0, _buffer, _offset + _count, length);
            _count += length;
        }

        /// <summary>
        /// 尝试取出一整帧。成功返回 true，frameOffset/frameLength 指向 Buffer 内的包体。
        /// </summary>
        public bool TryReadFrame(out int frameOffset, out int frameLength)
        {
            frameOffset = 0;
            frameLength = 0;

            if (_expectedLength < 0)
            {
                if (_count < 4) return false;
                // 线上固定小端
                _expectedLength = _buffer[_offset]
                    | (_buffer[_offset + 1] << 8)
                    | (_buffer[_offset + 2] << 16)
                    | (_buffer[_offset + 3] << 24);
                _offset += 4;
                _count -= 4;
            }

            if (_count < _expectedLength) return false;

            frameOffset = _offset;
            frameLength = _expectedLength;
            _offset += _expectedLength;
            _count -= _expectedLength;
            _expectedLength = -1;
            return true;
        }

        private void EnsureCapacity(int incomingLength)
        {
            if (_offset + _count + incomingLength <= _buffer.Length)
                return;

            // compact：把未消费数据移到头部
            if (_count + incomingLength <= _buffer.Length)
            {
                Array.Copy(_buffer, _offset, _buffer, 0, _count);
                _offset = 0;
                return;
            }

            // 翻倍扩容
            var newBuffer = new byte[Math.Max(_buffer.Length * 2, _count + incomingLength)];
            Array.Copy(_buffer, _offset, newBuffer, 0, _count);
            _buffer = newBuffer;
            _offset = 0;
        }
    }
}
