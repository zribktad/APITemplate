using APITemplate.Application.DTOs;
using APITemplate.Application.Validators;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Validators;

public class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _sut = new();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var request = new CreateProductRequest("Test Product", "Description", 9.99m);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyName_IsInvalid(string? name)
    {
        var request = new CreateProductRequest(name!, "Description", 9.99m);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_NameExceeds200Characters_IsInvalid()
    {
        var longName = new string('A', 201);
        var request = new CreateProductRequest(longName, null, 9.99m);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public void Validate_PriceZeroOrNegative_IsInvalid(decimal price)
    {
        var request = new CreateProductRequest("Valid Name", null, price);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Price");
    }

    [Fact]
    public void Validate_NullDescription_IsValid()
    {
        var request = new CreateProductRequest("Valid Name", null, 9.99m);

        var result = _sut.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
