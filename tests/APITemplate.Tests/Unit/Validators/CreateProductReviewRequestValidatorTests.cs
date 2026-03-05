using APITemplate.Application.Features.ProductReview.Validation;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Validators;

public class CreateProductReviewRequestValidatorTests
{
    private readonly CreateProductReviewRequestValidator _validator = new();

    [Fact]
    public async Task Validate_ValidRequest_Passes()
    {
        var request = new CreateProductReviewRequest(Guid.NewGuid(), "Great product!", 5);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Validate_InvalidRating_Fails(int rating)
    {
        var request = new CreateProductReviewRequest(Guid.NewGuid(), null, rating);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Rating");
    }

    [Fact]
    public async Task Validate_EmptyProductId_Fails()
    {
        var request = new CreateProductReviewRequest(Guid.Empty, null, 3);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ProductId");
    }
}
