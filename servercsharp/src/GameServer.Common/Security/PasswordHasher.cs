using System.Security.Cryptography;
using System.Text;

namespace GameServer.Common.Security;

/// <summary>
/// 密码哈希与验证 — 移植自 common.lua password_hash/password_verify
/// 格式: base64(salt) ":" MD5(salt + password)
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var saltedPassword = new byte[salt.Length + Encoding.UTF8.GetByteCount(password)];
        Buffer.BlockCopy(salt, 0, saltedPassword, 0, salt.Length);
        Encoding.UTF8.GetBytes(password, 0, password.Length, saltedPassword, salt.Length);
        var hash = MD5.HashData(saltedPassword);
        return Convert.ToBase64String(salt) + ":" + Convert.ToHexString(hash).ToLower();
    }

    public static bool Verify(string password, string storedHash)
    {
        var sep = storedHash.IndexOf(':');
        if (sep < 0) return false;

        var saltB64 = storedHash[..sep];
        var hashHex = storedHash[(sep + 1)..];
        byte[] salt;
        try { salt = Convert.FromBase64String(saltB64); }
        catch { return false; }

        var computed = MD5.HashData(salt.Concat(Encoding.UTF8.GetBytes(password)).ToArray());
        return Convert.ToHexString(computed).ToLower() == hashHex;
    }
}
