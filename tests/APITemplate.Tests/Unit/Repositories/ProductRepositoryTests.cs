using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
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
    public async Task AsQueryable_ReturnsProducts()
    {
        var p1 = CreateProduct("Alpha", 10m);
        var p2 = CreateProduct("Beta", 20m);
        _dbContext.Products.AddRange(p1, p2);
        await _dbContext.SaveChangesAsync();

        var result = _sut.AsQueryable().ToList();

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void AsQueryable_WhenEmpty_ReturnsEmptyQueryable()
    {
        var result = _sut.AsQueryable().ToList();

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
        await _dbContext.SaveChangesAsync();

        var deleted = await _dbContext.Products.FindAsync(product.Id);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ThrowsNotFoundException()
    {
        var act = () => _sut.DeleteAsync(Guid.NewGuid());

        await Should.ThrowAsync<NotFoundException>(act);
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
