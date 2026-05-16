using BCryptNet = BCrypt.Net.BCrypt;

namespace GameServer.Common.Security;

/// <summary>
/// 密码哈希与验证 — 安全升级版
/// 新密码使用 bcrypt (work factor 11)
/// 向后兼容：检测到旧 MD5 格式（含冒号分隔符）时，登录成功后自动迁移到 bcrypt
/// </summary>
public static class PasswordHasher
{
    /// <summary>
    /// 判断是否是旧版 MD5 哈希格式（base64(salt):hex(md5)）
    /// </summary>
    public static bool IsLegacyHash(string storedHash)
        => storedHash.Contains(':') && storedHash.Length < 100;

    /// <summary>
    /// 生成 bcrypt 哈希
    /// </summary>
    public static string Hash(string password)
        => BCryptNet.HashPassword(password, workFactor: 11);

    /// <summary>
    /// 验证密码。兼容旧 MD5 格式，验证成功返回 true，并建议迁移。
    /// </summary>
    public static bool Verify(string password, string storedHash, out bool shouldMigrate)
    {
        shouldMigrate = false;

        // 旧版 MD5 格式
        if (IsLegacyHash(storedHash))
        {
            if (VerifyLegacyMd5(password, storedHash))
            {
                shouldMigrate = true;
                return true;
            }
            return false;
        }

        // bcrypt 格式
        try { return BCryptNet.Verify(password, storedHash); }
        catch (BCrypt.Net.SaltParseException) { return false; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>
    /// 旧版 MD5 验证（向后兼容）
    /// 格式: base64(salt) ":" MD5(salt + password)
    /// </summary>
    private static bool VerifyLegacyMd5(string password, string storedHash)
    {
        var sep = storedHash.IndexOf(':');
        if (sep < 0) return false;

        var saltB64 = storedHash[..sep];
        var hashHex = storedHash[(sep + 1)..];
        byte[] salt;
        try { salt = Convert.FromBase64String(saltB64); }
        catch { return false; }

        using var md5 = System.Security.Cryptography.MD5.Create();
        var computed = md5.ComputeHash(salt.Concat(System.Text.Encoding.UTF8.GetBytes(password)).ToArray());
        return Convert.ToHexString(computed).ToLower() == hashHex;
    }
}
