using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    // Add product-specific query methods here, e.g.:
    // Task<IReadOnlyList<Product>> GetByNameAsync(string name, CancellationToken ct = default);
}
