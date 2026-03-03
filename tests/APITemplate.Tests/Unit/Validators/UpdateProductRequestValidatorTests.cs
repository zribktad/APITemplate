using System.ComponentModel.DataAnnotations;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Validators;

public class UpdateProductRequestValidatorTests
{
    private readonly UpdateProductRequestValidator _sut = new();

    // --- Data Annotation tests ([NotEmpty], [MaxLength], [Range]) ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Annotation_InvalidName_IsInvalid(string? name)
    {
        var request = new UpdateProductRequest(name!, null, 19.99m);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains("Name"));
    }

    [Fact]
    public void Annotation_NameExceeds200Characters_IsInvalid()
    {
        var request = new UpdateProductRequest(new string('A', 201), null, 19.99m);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains("Name"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50.25)]
    public void Annotation_PriceZeroOrNegative_IsInvalid(decimal price)
    {
        var request = new UpdateProductRequest("Valid Name", null, price);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains("Price"));
    }

    // --- FluentValidation tests (cross-field rules) ---

    [Fact]
    public void FluentValidation_PriceAbove1000_WithoutDescription_IsInvalid()
    {
        var result = _sut.Validate(new UpdateProductRequest("Premium", null, 1001m));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Description");
    }

    [Fact]
    public void FluentValidation_PriceAbove1000_WithDescription_IsValid()
    {
        var result = _sut.Validate(new UpdateProductRequest("Premium", "Detailed description", 1001m));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void FluentValidation_PriceBelow1000_WithoutDescription_IsValid()
    {
        var result = _sut.Validate(new UpdateProductRequest("Standard", null, 999m));

        result.IsValid.ShouldBeTrue();
    }
}
