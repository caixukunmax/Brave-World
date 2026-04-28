using Xunit;
using GameServer.Common.Security;

namespace GameServer.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_And_Verify_Should_Succeed()
    {
        var password = "test_password_123";
        var hash = PasswordHasher.Hash(password);
        Assert.True(PasswordHasher.Verify(password, hash));
    }

    [Fact]
    public void Verify_Wrong_Password_Should_Fail()
    {
        var hash = PasswordHasher.Hash("correct_password");
        Assert.False(PasswordHasher.Verify("wrong_password", hash));
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
        Assert.False(PasswordHasher.Verify("password", "no_colon_here"));
        Assert.False(PasswordHasher.Verify("password", ""));
    }
}
