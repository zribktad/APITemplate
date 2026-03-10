using APITemplate.Application.Common.Sorting;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product;
public static class ProductSortFields
{
    public static readonly SortField Name = new("name");
    public static readonly SortField Price = new("price");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<ProductEntity> Map = new SortFieldMap<ProductEntity>()
        .Add(Name, p => p.Name)
        .Add(Price, p => (object)p.Price)
        .Add(CreatedAt, p => p.Audit.CreatedAtUtc)
        .Default(p => p.Audit.CreatedAtUtc);
}
