using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using APITemplate.Application.Services;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Services;

public class TokenServiceTests
{
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpirationMinutes"] = "60"
            })
            .Build();

        _sut = new TokenService(configuration);
    }

    [Fact]
    public void GenerateToken_ReturnsValidToken()
    {
        var result = _sut.GenerateToken("testuser");

        result.ShouldNotBeNull();
        result.AccessToken.ShouldNotBeNullOrWhiteSpace();
        result.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateToken_TokenContainsCorrectClaims()
    {
        var result = _sut.GenerateToken("testuser");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.AccessToken);

        jwt.Issuer.ShouldBe("TestIssuer");
        jwt.Audiences.ShouldContain("TestAudience");
        jwt.Claims.ShouldContain(c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        jwt.Claims.ShouldContain(c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void GenerateToken_ExpiresAtIsApproximately60MinutesFromNow()
    {
        var before = DateTime.UtcNow.AddMinutes(59);
        var result = _sut.GenerateToken("testuser");
        var after = DateTime.UtcNow.AddMinutes(61);

        result.ExpiresAt.ShouldBeGreaterThan(before);
        result.ExpiresAt.ShouldBeLessThan(after);
    }

    [Fact]
    public void GenerateToken_DifferentUsersGetDifferentTokens()
    {
        var token1 = _sut.GenerateToken("user1");
        var token2 = _sut.GenerateToken("user2");

        token1.AccessToken.ShouldNotBe(token2.AccessToken);
    }
}
