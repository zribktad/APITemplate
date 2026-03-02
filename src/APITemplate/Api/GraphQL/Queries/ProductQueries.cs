using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using HotChocolate.Types;

namespace APITemplate.Api.GraphQL.Queries;

public class ProductQueries
{
    [UsePaging(MaxPageSize = 100, DefaultPageSize = 20)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Product> GetProducts([Service] IProductRepository repo)
        => repo.AsQueryable();

    [UseFirstOrDefault]
    [UseProjection]
    public IQueryable<Product> GetProductById(
        Guid id,
        [Service] IProductRepository repo)
        => repo.AsQueryable().Where(p => p.Id == id);
}
