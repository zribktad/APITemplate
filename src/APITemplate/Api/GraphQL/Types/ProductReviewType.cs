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

        descriptor.Field(r => r.UserId)
            .Type<NonNullType<UuidType>>()
            .Description("The identifier of the user who wrote the review.");

        descriptor.Field(r => r.Rating)
            .Type<NonNullType<IntType>>()
            .Description("Rating from 1 to 5.");

        descriptor.Field(r => r.Comment)
            .Description("The optional review comment.");

        descriptor.Field(r => r.CreatedAtUtc)
            .Description("The UTC timestamp of when the review was created.");
    }
}
