using GameServer.Common;
using GameServer.Common.Net;
using GameServer.Services.Core;
using Google.Protobuf;
using PCommon = global::Common;
using PGame = global::Game;
using PProtocol = global::Protocol;

namespace GameServer.Services.Player.Handlers;

/// <summary>
/// 返回服务器当前正在写入的日志文件绝对路径 — 供客户端截屏工具调试定位使用。
/// </summary>
[HandlesMessage((int)PProtocol.MessageId.GameGetServerLogPathReq)]
public class ServerLogPathHandler : IMessageHandler
{
    public Task<byte[]?> HandleAsync(MessageContext ctx, byte[] data)
    {
        var rsp = new PGame.GetServerLogPathResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "ok",
            LogPath = ServerLogConfig.GetCurrentLogFilePath(),
        };
        return Task.FromResult<byte[]?>(rsp.ToByteArray());
    }
}
