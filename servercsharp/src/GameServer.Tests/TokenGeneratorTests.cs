using Xunit;
using GameServer.Common.Security;

namespace GameServer.Tests;

public class TokenGeneratorTests
{
    private const string TestSecret = "test_secret_key_123";

    [Fact]
    public void AccountToken_Generate_And_Validate_Should_Succeed()
    {
        var gen = new TokenGenerator(TestSecret);
        var token = gen.GenerateAccountToken(accountId: 42, username: "player1");
        var claims = gen.ValidateAccountToken(token);
        Assert.NotNull(claims);
        Assert.Equal(42, claims!.AccountId);
        Assert.Equal("player1", claims.Username);
    }

    [Fact]
    public void AccountToken_Invalid_Token_Should_Return_Null()
    {
        var gen = new TokenGenerator(TestSecret);
        Assert.Null(gen.ValidateAccountToken(""));
        Assert.Null(gen.ValidateAccountToken("not_base64!!!"));
        Assert.Null(gen.ValidateAccountToken(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("no_pipe"))));
    }

    [Fact]
    public void AccountToken_Wrong_Secret_Should_Fail()
    {
        var gen1 = new TokenGenerator("secret_a");
        var gen2 = new TokenGenerator("secret_b");
        var token = gen1.GenerateAccountToken(1, "user");
        Assert.Null(gen2.ValidateAccountToken(token));
    }

    [Fact]
    public void GatewayToken_Generate_And_Validate_Should_Succeed()
    {
        var gen = new TokenGenerator(TestSecret);
        var token = gen.GenerateGatewayToken(accountId: 100, serverId: 5);
        var claims = gen.ValidateGatewayToken(token);
        Assert.NotNull(claims);
        Assert.Equal(100, claims!.AccountId);
        Assert.Equal(5, claims.ServerId);
    }
}
