using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Services;

public class UserServiceTests
{
    private readonly UserService _sut;

    public UserServiceTests()
    {
        var options = Options.Create(
            new AuthOptions
            {
                Username = "admin",
                Password = "admin"
            });

        _sut = new UserService(options);
    }

    [Fact]
    public async Task ValidateAsync_CorrectCredentials_ReturnsTrue()
    {
        var result = await _sut.ValidateAsync("admin", "admin");

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_UsernameIsCaseInsensitive_ReturnsTrue()
    {
        var result = await _sut.ValidateAsync("ADMIN", "admin");

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_InvalidCredentials_ReturnsFalse()
    {
        var result = await _sut.ValidateAsync("admin", "wrong");

        result.ShouldBeFalse();
    }
}
