using APITemplate.Application.DTOs;
using APITemplate.Application.Validators;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Validators;

public class UpdateProductRequestValidatorTests
{
    private readonly UpdateProductRequestValidator _sut = new();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var request = new UpdateProductRequest("Updated Product", "Description", 19.99m);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyName_IsInvalid(string? name)
    {
        var request = new UpdateProductRequest(name!, "Description", 19.99m);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_NameExceeds200Characters_IsInvalid()
    {
        var longName = new string('A', 201);
        var request = new UpdateProductRequest(longName, null, 19.99m);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50.25)]
    public void Validate_PriceZeroOrNegative_IsInvalid(decimal price)
    {
        var request = new UpdateProductRequest("Valid Name", null, price);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Price");
    }
}
