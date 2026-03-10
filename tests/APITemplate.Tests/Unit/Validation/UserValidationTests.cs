using APITemplate.Application.Features.User.DTOs;
using APITemplate.Application.Features.User.Validation;
using APITemplate.Domain.Enums;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Validation;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _sut = new();

    [Fact]
    public void ValidRequest_IsValid()
    {
        var result = _sut.Validate(new CreateUserRequest("validuser", "valid@email.com", "Password1!"));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyUsername_IsInvalid(string? username)
    {
        var result = _sut.Validate(new CreateUserRequest(username!, "valid@email.com", "Password1!"));

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void InvalidEmail_IsInvalid(string? email)
    {
        var result = _sut.Validate(new CreateUserRequest("user", email!, "Password1!"));

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void TooShortOrEmptyPassword_IsInvalid(string? password)
    {
        var result = _sut.Validate(new CreateUserRequest("user", "valid@email.com", password!));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void NullPassword_StopsAfterEmptyValidation()
    {
        var result = _sut.Validate(new CreateUserRequest("user", "valid@email.com", null!));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotContain(e => e.ErrorMessage.Contains("uppercase"));
        result.Errors.ShouldNotContain(e => e.ErrorMessage.Contains("digit"));
        result.Errors.ShouldNotContain(e => e.ErrorMessage.Contains("special"));
    }

    [Fact]
    public void PasswordWithoutUppercase_IsInvalid()
    {
        var result = _sut.Validate(new CreateUserRequest("user", "valid@email.com", "password1!"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("uppercase"));
    }

    [Fact]
    public void PasswordWithoutDigit_IsInvalid()
    {
        var result = _sut.Validate(new CreateUserRequest("user", "valid@email.com", "Password!!"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("digit"));
    }

    [Fact]
    public void PasswordWithoutSpecialChar_IsInvalid()
    {
        var result = _sut.Validate(new CreateUserRequest("user", "valid@email.com", "Password12"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("special"));
    }
}

public class UpdateUserRequestValidatorTests
{
    private readonly UpdateUserRequestValidator _sut = new();

    [Fact]
    public void ValidRequest_IsValid()
    {
        var result = _sut.Validate(new UpdateUserRequest("validuser", "valid@email.com"));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyUsername_IsInvalid(string? username)
    {
        var result = _sut.Validate(new UpdateUserRequest(username!, "valid@email.com"));

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void InvalidEmail_IsInvalid(string? email)
    {
        var result = _sut.Validate(new UpdateUserRequest("user", email!));

        result.IsValid.ShouldBeFalse();
    }
}

public class ChangeUserRoleRequestValidatorTests
{
    private readonly ChangeUserRoleRequestValidator _sut = new();

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.PlatformAdmin)]
    public void ValidRole_IsValid(UserRole role)
    {
        var result = _sut.Validate(new ChangeUserRoleRequest(role));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void InvalidRole_IsInvalid()
    {
        var result = _sut.Validate(new ChangeUserRoleRequest((UserRole)999));

        result.IsValid.ShouldBeFalse();
    }
}
