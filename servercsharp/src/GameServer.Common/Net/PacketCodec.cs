using Google.Protobuf;
using Packet = global::Common.Packet;

namespace GameServer.Common.Net;

/// <summary>
/// Protobuf 包编解码
/// 线格式: [4字节 LE 长度][common.Packet protobuf body]
/// </summary>
public static class PacketCodec
{
    public const int MaxPacketSize = 65536; // 64KB

    /// <summary>
    /// 编码: 将 Packet 序列化并添加 4 字节 LE 长度前缀
    /// </summary>
    public static byte[] Encode(Packet packet)
    {
        var body = packet.ToByteArray();
        var header = new byte[4];
        header[0] = (byte)(body.Length & 0xFF);
        header[1] = (byte)((body.Length >> 8) & 0xFF);
        header[2] = (byte)((body.Length >> 16) & 0xFF);
        header[3] = (byte)((body.Length >> 24) & 0xFF);

        var result = new byte[4 + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, 4);
        Buffer.BlockCopy(body, 0, result, 4, body.Length);
        return result;
    }

    /// <summary>
    /// 构造 Packet
    /// </summary>
    public static Packet MakePacket(int msgId, uint session, byte[] data)
    {
        return new Packet
        {
            MsgId = (uint)msgId,
            Session = session,
            Data = ByteString.CopyFrom(data),
            Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
    }

    /// <summary>
    /// 尝试从缓冲区读取一个完整的 Packet
    /// 返回: (packet, consumedBytes) 或 null
    /// </summary>
    public static (Packet packet, int consumedBytes)? TryDecode(byte[] buffer, int length)
    {
        if (length < 4) return null;

        int bodyLen = buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24);
        if (bodyLen <= 0 || bodyLen > MaxPacketSize)
            throw new InvalidDataException($"Invalid packet size: {bodyLen}");

        if (length < 4 + bodyLen) return null;

        var packet = Packet.Parser.ParseFrom(buffer, 4, bodyLen);
        return (packet, 4 + bodyLen);
    }
}
