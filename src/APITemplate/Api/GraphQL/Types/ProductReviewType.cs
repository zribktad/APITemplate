using APITemplate.Application.DTOs;
using HotChocolate.Types;

namespace APITemplate.Api.GraphQL.Types;

public sealed class ProductReviewType : ObjectType<ProductReviewResponse>
{
    protected override void Configure(IObjectTypeDescriptor<ProductReviewResponse> descriptor)
    {
        descriptor.Description("Represents a review for a product.");

        descriptor.Field(r => r.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the review.");

        descriptor.Field(r => r.ProductId)
            .Type<NonNullType<UuidType>>()
            .Description("The identifier of the reviewed product.");

        descriptor.Field(r => r.ReviewerName)
            .Type<NonNullType<StringType>>()
            .Description("The name of the reviewer.");

        descriptor.Field(r => r.Rating)
            .Type<NonNullType<IntType>>()
            .Description("Rating from 1 to 5.");
    }
}
