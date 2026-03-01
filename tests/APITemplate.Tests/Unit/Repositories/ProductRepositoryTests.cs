using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Repositories;

public class ProductRepositoryTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ProductRepository _sut;

    public ProductRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _sut = new ProductRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AddAsync_PersistsProduct()
    {
        var product = CreateProduct("Test Product", 10m);

        var result = await _sut.AddAsync(product);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(product.Id);

        var persisted = await _dbContext.Products.FindAsync(product.Id);
        persisted.ShouldNotBeNull();
        persisted!.Name.ShouldBe("Test Product");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsProduct()
    {
        var product = CreateProduct("Existing", 5m);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(product.Id);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("Existing");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProductsOrderedByCreatedAtDesc()
    {
        var older = CreateProduct("Older", 10m);
        older.CreatedAt = DateTime.UtcNow.AddDays(-1);

        var newer = CreateProduct("Newer", 20m);
        newer.CreatedAt = DateTime.UtcNow;

        _dbContext.Products.AddRange(older, newer);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("Newer");
        result[1].Name.ShouldBe("Older");
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var result = await _sut.GetAllAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ModifiesProduct()
    {
        var product = CreateProduct("Original", 10m);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();
        _dbContext.Entry(product).State = EntityState.Detached;

        product.Name = "Updated";
        product.Price = 25m;
        await _sut.UpdateAsync(product);

        var updated = await _dbContext.Products.FindAsync(product.Id);
        updated!.Name.ShouldBe("Updated");
        updated.Price.ShouldBe(25m);
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_RemovesProduct()
    {
        var product = CreateProduct("ToDelete", 10m);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        await _sut.DeleteAsync(product.Id);

        var deleted = await _dbContext.Products.FindAsync(product.Id);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_DoesNotThrow()
    {
        var act = () => _sut.DeleteAsync(Guid.NewGuid());

        await Should.NotThrowAsync(act);
    }

    private static Product CreateProduct(string name, decimal price)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Price = price,
            CreatedAt = DateTime.UtcNow
        };
    }
}
