using APITemplate.Application.DTOs;
using HotChocolate.Types;

namespace APITemplate.Api.GraphQL.Types;

public sealed class ProductType : ObjectType<ProductResponse>
{
    protected override void Configure(IObjectTypeDescriptor<ProductResponse> descriptor)
    {
        descriptor.Description("Represents a product in the catalog.");

        descriptor.Field(p => p.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the product.");

        descriptor.Field(p => p.Name)
            .Type<NonNullType<StringType>>()
            .Description("The name of the product.");

        descriptor.Field(p => p.Price)
            .Type<NonNullType<DecimalType>>()
            .Description("The price of the product.");
    }
}
