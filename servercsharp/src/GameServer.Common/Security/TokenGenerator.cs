using System.Security.Cryptography;
using System.Text;

namespace GameServer.Common.Security;

/// <summary>
/// Token 生成与验证 — 移植自 common.lua token_generate/token_validate
/// AccountToken: base64(account_id:username:timestamp|MD5(secret+payload)), TTL 300s
/// GatewayToken: base64(account_id:server_id:timestamp|MD5(secret+payload)), TTL 604800s
/// </summary>
public class TokenGenerator
{
    private readonly string _secret;

    public TokenGenerator(string secret)
    {
        _secret = secret;
    }

    // ---- Account Token ----

    public string GenerateAccountToken(long accountId, string username)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = $"{accountId}:{username}:{ts}";
        var sig = Md5Hex(_secret + payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload + "|" + sig));
    }

    public AccountTokenClaims? ValidateAccountToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        string decoded;
        try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token)); }
        catch { return null; }

        var pipeIdx = decoded.LastIndexOf('|');
        if (pipeIdx < 0) return null;

        var payload = decoded[..pipeIdx];
        var sig = decoded[(pipeIdx + 1)..];

        if (Md5Hex(_secret + payload) != sig) return null;

        var parts = payload.Split(':');
        if (parts.Length != 3) return null;
        if (!long.TryParse(parts[0], out var aid)) return null;
        if (!long.TryParse(parts[2], out var ts)) return null;

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts > 300) return null;

        return new AccountTokenClaims { AccountId = aid, Username = parts[1] };
    }

    // ---- Gateway Token ----

    public string GenerateGatewayToken(long accountId, int serverId)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = $"{accountId}:{serverId}:{ts}";
        var sig = Md5Hex(_secret + payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload + "|" + sig));
    }

    public GatewayTokenClaims? ValidateGatewayToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        string decoded;
        try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token)); }
        catch { return null; }

        var pipeIdx = decoded.LastIndexOf('|');
        if (pipeIdx < 0) return null;

        var payload = decoded[..pipeIdx];
        var sig = decoded[(pipeIdx + 1)..];

        if (Md5Hex(_secret + payload) != sig) return null;

        var parts = payload.Split(':');
        if (parts.Length != 3) return null;
        if (!long.TryParse(parts[0], out var aid)) return null;
        if (!int.TryParse(parts[1], out var sid)) return null;
        if (!long.TryParse(parts[2], out var ts)) return null;

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts > 604800) return null;

        return new GatewayTokenClaims { AccountId = aid, ServerId = sid };
    }

    private static string Md5Hex(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLower();
    }
}

public record AccountTokenClaims
{
    public long AccountId { get; init; }
    public string Username { get; init; } = "";
}

public record GatewayTokenClaims
{
    public long AccountId { get; init; }
    public int ServerId { get; init; }
}
