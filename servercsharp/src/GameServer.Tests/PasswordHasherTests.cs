using Xunit;
using GameServer.Common.Security;

namespace GameServer.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_And_Verify_Bcrypt_Should_Succeed()
    {
        var password = "test_password_123";
        var hash = PasswordHasher.Hash(password);
        bool shouldMigrate;
        Assert.True(PasswordHasher.Verify(password, hash, out shouldMigrate));
        Assert.False(shouldMigrate); // bcrypt 不需要迁移
    }

    [Fact]
    public void Verify_Wrong_Password_Should_Fail()
    {
        var hash = PasswordHasher.Hash("correct_password");
        bool shouldMigrate;
        Assert.False(PasswordHasher.Verify("wrong_password", hash, out shouldMigrate));
    }

    [Fact]
    public void Hash_Should_Be_Different_Each_Time()
    {
        var password = "same_password";
        var hash1 = PasswordHasher.Hash(password);
        var hash2 = PasswordHasher.Hash(password);
        Assert.NotEqual(hash1, hash2); // 不同 salt
    }

    [Fact]
    public void Verify_Invalid_Format_Should_Fail()
    {
        bool shouldMigrate;
        Assert.False(PasswordHasher.Verify("password", "no_colon_here", out shouldMigrate));
        Assert.False(PasswordHasher.Verify("password", "", out shouldMigrate));
    }

    [Fact]
    public void Verify_Legacy_Md5_Should_Succeed_And_Suggest_Migration()
    {
        bool shouldMigrate;
        // 构造一个已知能通过验证的 legacy hash (base64(salt):hex(md5(salt+password)))
        var salt = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test_salt_16b"));
        using var md5 = System.Security.Cryptography.MD5.Create();
        var computed = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("test_salt_16b" + "mypassword"));
        var validLegacy = salt + ":" + System.Convert.ToHexString(computed).ToLower();

        Assert.True(PasswordHasher.Verify("mypassword", validLegacy, out shouldMigrate));
        Assert.True(shouldMigrate); // legacy 需要迁移到 bcrypt
    }

    [Fact]
    public void IsLegacyHash_Should_Detect_Old_Format()
    {
        Assert.True(PasswordHasher.IsLegacyHash("abc:def"));
        Assert.False(PasswordHasher.IsLegacyHash("$2a$11$xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")); // bcrypt
    }
}
