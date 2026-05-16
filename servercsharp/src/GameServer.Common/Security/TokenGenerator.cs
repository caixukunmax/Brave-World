using System.Security.Cryptography;
using System.Text;

namespace GameServer.Common.Security;

/// <summary>
/// Token 生成与验证 — 安全升级版
/// AccountToken: base64(account_id:username:timestamp|HMAC-SHA256(secret,payload)), TTL 300s
/// GatewayToken: base64(account_id:server_id:timestamp|HMAC-SHA256(secret,payload)), TTL 7200s (2h)
/// </summary>
public class TokenGenerator
{
    private readonly byte[] _secretKey;

    public TokenGenerator(string secret)
    {
        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    // ---- Account Token ----

    public string GenerateAccountToken(long accountId, string username)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = $"{accountId}:{username}:{ts}";
        var sig = HmacSha256Hex(payload);
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

        if (HmacSha256Hex(payload) != sig) return null;

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
        var sig = HmacSha256Hex(payload);
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

        if (HmacSha256Hex(payload) != sig) return null;

        var parts = payload.Split(':');
        if (parts.Length != 3) return null;
        if (!long.TryParse(parts[0], out var aid)) return null;
        if (!int.TryParse(parts[1], out var sid)) return null;
        if (!long.TryParse(parts[2], out var ts)) return null;

        // TTL: 2 hours (was 7 days / 604800s)
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts > 7200) return null;

        return new GatewayTokenClaims { AccountId = aid, ServerId = sid };
    }

    private string HmacSha256Hex(string payload)
    {
        using var hmac = new HMACSHA256(_secretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
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
