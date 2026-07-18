using System;
using System.Collections.Generic;
using Google.Protobuf;
using NUnit.Framework;
using Protocol;
using UnityClientSharp.Net;

namespace BraveWorld.Tests
{
    /// <summary>封包帧拆解器（PacketFramer）测试：帧格式与 Godot/服务器一致——[4字节小端长度][Common.Packet]。</summary>
    public class NetworkFramingTests
    {
        private static byte[] BuildFrame(uint msgId, uint session, byte[] payload)
        {
            var packet = new Common.Packet
            {
                MsgId = msgId,
                Session = session,
                Data = Google.Protobuf.ByteString.CopyFrom(payload),
                Timestamp = 12345,
            };
            var body = packet.ToByteArray();
            var frame = new byte[4 + body.Length];
            frame[0] = (byte)(body.Length & 0xFF);
            frame[1] = (byte)((body.Length >> 8) & 0xFF);
            frame[2] = (byte)((body.Length >> 16) & 0xFF);
            frame[3] = (byte)((body.Length >> 24) & 0xFF);
            Array.Copy(body, 0, frame, 4, body.Length);
            return frame;
        }

        private static Common.Packet ReadPacket(PacketFramer framer, int offset, int length)
            => Common.Packet.Parser.ParseFrom(framer.Buffer, offset, length);

        [Test]
        public void RoundTrip_SinglePacket()
        {
            var framer = new PacketFramer();
            var payload = new byte[] { 1, 2, 3 };
            var frame = BuildFrame(100, 7, payload);
            framer.Write(frame, frame.Length);

            Assert.IsTrue(framer.TryReadFrame(out int offset, out int length));
            var packet = ReadPacket(framer, offset, length);
            Assert.AreEqual(100u, packet.MsgId);
            Assert.AreEqual(7u, packet.Session);
            Assert.AreEqual(12345u, packet.Timestamp);
            CollectionAssert.AreEqual(payload, packet.Data.ToByteArray());
            Assert.IsFalse(framer.TryReadFrame(out _, out _));
        }

        [Test]
        public void PartialFrame_HeaderThenBody()
        {
            var framer = new PacketFramer();
            var frame = BuildFrame(210, 1, new byte[] { 9 });

            // 先喂 2 字节头
            Assert.IsFalse(framer.TryReadFrame(out _, out _));
            framer.Write(frame, 2);
            Assert.IsFalse(framer.TryReadFrame(out _, out _));
            // 再喂剩余
            framer.Write(SubArray(frame, 2), frame.Length - 2);
            Assert.IsTrue(framer.TryReadFrame(out int offset, out int length));
            Assert.AreEqual(210u, ReadPacket(framer, offset, length).MsgId);
        }

        [Test]
        public void MultiplePackets_OneChunk()
        {
            var framer = new PacketFramer();
            var f1 = BuildFrame(1, 1, new byte[] { 1 });
            var f2 = BuildFrame(2, 2, new byte[] { 2, 2 });
            var f3 = BuildFrame(3, 3, new byte[] { 3, 3, 3 });
            var all = Concat(f1, f2, f3);
            framer.Write(all, all.Length);

            var ids = new List<uint>();
            while (framer.TryReadFrame(out int offset, out int length))
                ids.Add(ReadPacket(framer, offset, length).MsgId);
            CollectionAssert.AreEqual(new uint[] { 1, 2, 3 }, ids);
        }

        [Test]
        public void OversizedPacket_GrowsBuffer()
        {
            var framer = new PacketFramer();
            var payload = new byte[20000]; // 超过 8KB 初始缓冲
            new Random(42).NextBytes(payload);
            var frame = BuildFrame(360, 99, payload);

            // 分块喂入，强制 compact + 扩容
            int pos = 0;
            while (pos < frame.Length)
            {
                int n = Math.Min(1024, frame.Length - pos);
                framer.Write(SubArray(frame, pos, n), n);
                pos += n;
            }

            Assert.IsTrue(framer.TryReadFrame(out int offset, out int length));
            var packet = ReadPacket(framer, offset, length);
            Assert.AreEqual(360u, packet.MsgId);
            CollectionAssert.AreEqual(payload, packet.Data.ToByteArray());
        }

        [Test]
        public void Compaction_ReusesBufferAcrossPackets()
        {
            var framer = new PacketFramer();
            // 连续收发 200 个小包，验证 offset 推进与 compact 不丢数据
            for (uint i = 0; i < 200; i++)
            {
                var f = BuildFrame(i, i, new byte[] { (byte)(i % 256) });
                framer.Write(f, f.Length);
                Assert.IsTrue(framer.TryReadFrame(out int offset, out int length), $"packet {i} lost");
                Assert.AreEqual(i, ReadPacket(framer, offset, length).MsgId);
            }
        }

        private static byte[] SubArray(byte[] src, int offset)
            => SubArray(src, offset, src.Length - offset);

        private static byte[] SubArray(byte[] src, int offset, int length)
        {
            var dst = new byte[length];
            Array.Copy(src, offset, dst, 0, length);
            return dst;
        }

        private static byte[] Concat(params byte[][] arrays)
        {
            int total = 0;
            foreach (var a in arrays) total += a.Length;
            var dst = new byte[total];
            int pos = 0;
            foreach (var a in arrays)
            {
                Array.Copy(a, 0, dst, pos, a.Length);
                pos += a.Length;
            }
            return dst;
        }
    }
}
