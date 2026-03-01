using APITemplate.Application.DTOs;
using APITemplate.Application.Validators;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _sut = new();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var request = new LoginRequest("admin", "password");

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyUsername_IsInvalid(string? username)
    {
        var request = new LoginRequest(username!, "password");

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Username");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyPassword_IsInvalid(string? password)
    {
        var request = new LoginRequest("admin", password!);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void Validate_BothEmpty_HasMultipleErrors()
    {
        var request = new LoginRequest("", "");

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
    }
}
