using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using MongoDB.Driver;

namespace APITemplate.Infrastructure.Repositories;

public sealed class ProductDataRepository : IProductDataRepository
{
    private readonly IMongoCollection<ProductData> _collection;

    public ProductDataRepository(MongoDbContext context)
    {
        _collection = context.ProductData;
    }

    public async Task<ProductData?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _collection
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

    public async Task<List<ProductData>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idArray = ids
            .Distinct()
            .ToArray();

        if (idArray.Length == 0)
            return [];

        return await _collection
            .Find(Builders<ProductData>.Filter.And(
                Builders<ProductData>.Filter.In(x => x.Id, idArray),
                Builders<ProductData>.Filter.Eq(x => x.IsDeleted, false)))
            .ToListAsync(ct);
    }

    public async Task<List<ProductData>> GetAllAsync(string? type = null, CancellationToken ct = default)
    {
        var filter = type is null
            ? Builders<ProductData>.Filter.Eq(x => x.IsDeleted, false)
            : Builders<ProductData>.Filter.And(
                Builders<ProductData>.Filter.Eq("_t", type),
                Builders<ProductData>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter).ToListAsync(ct);
    }

    public async Task<ProductData> CreateAsync(ProductData productData, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(productData, cancellationToken: ct);
        return productData;
    }

    public async Task SoftDeleteAsync(Guid id, Guid actorId, DateTime deletedAtUtc, CancellationToken ct = default)
    {
        var update = Builders<ProductData>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAtUtc, deletedAtUtc)
            .Set(x => x.DeletedBy, actorId);

        await _collection.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            update,
            cancellationToken: ct);
    }
}
