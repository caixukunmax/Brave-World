using Xunit;
using GameServer.Common.Net;
using Google.Protobuf;

namespace GameServer.Tests;

public class PacketCodecTests
{
    [Fact]
    public void Encode_And_Decode_Should_Roundtrip()
    {
        var packet = PacketCodec.MakePacket(
            msgId: 1001,
            session: 42,
            data: System.Text.Encoding.UTF8.GetBytes("hello world")
        );

        var encoded = PacketCodec.Encode(packet);
        var result = PacketCodec.TryDecode(encoded, encoded.Length);

        Assert.NotNull(result);
        Assert.Equal(packet.MsgId, result!.Value.packet.MsgId);
        Assert.Equal(packet.Session, result.Value.packet.Session);
        Assert.Equal(packet.Data, result.Value.packet.Data);
        Assert.Equal(encoded.Length, result.Value.consumedBytes);
    }

    [Fact]
    public void TryDecode_Incomplete_Buffer_Should_Return_Null()
    {
        var packet = PacketCodec.MakePacket(1, 0, new byte[100]);
        var encoded = PacketCodec.Encode(packet);

        // 只给前几个字节
        Assert.Null(PacketCodec.TryDecode(encoded, 3));
        Assert.Null(PacketCodec.TryDecode(encoded, 10));
    }

    [Fact]
    public void TryDecode_Invalid_Size_Should_Throw()
    {
        var buffer = new byte[4];
        // 构造一个超大 bodyLen
        buffer[0] = 0xFF; buffer[1] = 0xFF; buffer[2] = 0xFF; buffer[3] = 0x7F;
        Assert.Throws<InvalidDataException>(() => PacketCodec.TryDecode(buffer, 4));
    }
}
