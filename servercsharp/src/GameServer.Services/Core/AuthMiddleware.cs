using GameServer.Common.Net;
using GameServer.Common.Security;
using Google.Protobuf;
using PCommon = global::Common;

namespace GameServer.Services.Core;

/// <summary>
/// Token 验证中间件 — 从 PlayerManager 提取
/// </summary>
public class AuthMiddleware
{
    private readonly TokenGenerator _tokenGen;

    public AuthMiddleware(TokenGenerator tokenGen)
    {
        _tokenGen = tokenGen;
    }

    /// <summary>
    /// 验证 GatewayToken，返回 claims 或 null（验证失败）
    /// </summary>
    public GatewayTokenClaims? Validate(MessageContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.Token))
            return null;
        return _tokenGen.ValidateGatewayToken(ctx.Token);
    }

    public static byte[] MakeUnauthorized()
    {
        return new PCommon.Response { Code = PCommon.ErrorCode.Unauthorized, Message = "" }.ToByteArray();
    }
}
