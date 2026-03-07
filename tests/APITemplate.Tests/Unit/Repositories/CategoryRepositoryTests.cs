using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using APITemplate.Application.Common.Context;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Infrastructure.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Repositories;

public class CategoryRepositoryTests : IDisposable
{
    private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly AppDbContext _dbContext;
    private readonly Mock<IStoredProcedureExecutor> _spExecutorMock;
    private readonly ITenantProvider _tenantProvider;
    private readonly CategoryRepository _sut;

    public CategoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = new TestTenantProvider();
        _dbContext = new AppDbContext(options, _tenantProvider, new TestActorProvider());
        _spExecutorMock = new Mock<IStoredProcedureExecutor>();
        _sut = new CategoryRepository(_dbContext, _spExecutorMock.Object, _tenantProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AddAsync_PersistsCategory()
    {
        var category = CreateCategory("Electronics");

        var result = await _sut.AddAsync(category);
        await _dbContext.SaveChangesAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(category.Id);

        var persisted = await _dbContext.Categories.FindAsync(category.Id);
        persisted.ShouldNotBeNull();
        persisted!.Name.ShouldBe("Electronics");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsCategory()
    {
        var category = CreateCategory("Books");
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(category.Id);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("Books");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ModifiesCategory()
    {
        var category = CreateCategory("Old Name", "Old Description");
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();
        _dbContext.Entry(category).State = EntityState.Detached;

        category.Name = "New Name";
        category.Description = "New Description";
        await _sut.UpdateAsync(category);
        await _dbContext.SaveChangesAsync();

        var updated = await _dbContext.Categories.FindAsync(category.Id);
        updated!.Name.ShouldBe("New Name");
        updated.Description.ShouldBe("New Description");
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_RemovesCategory()
    {
        var category = CreateCategory("ToDelete");
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        await _sut.DeleteAsync(category.Id);
        await _dbContext.SaveChangesAsync();

        var deleted = await _dbContext.Categories.FindAsync(category.Id);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ThrowsNotFoundException()
    {
        var act = () => _sut.DeleteAsync(Guid.NewGuid());

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task GetStatsByIdAsync_WhenStatsExist_ReturnsStats()
    {
        var categoryId = Guid.NewGuid();
        var expected = new ProductCategoryStats
        {
            CategoryId = categoryId,
            CategoryName = "Electronics",
            ProductCount = 3,
            AveragePrice = 150m,
            TotalReviews = 10
        };

        _spExecutorMock
            .Setup(e => e.QueryFirstAsync(
                It.IsAny<GetProductCategoryStatsProcedure>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetStatsByIdAsync(categoryId);

        result.ShouldNotBeNull();
        result!.CategoryId.ShouldBe(categoryId);
        result.CategoryName.ShouldBe("Electronics");
        result.ProductCount.ShouldBe(3);
        result.AveragePrice.ShouldBe(150m);
        result.TotalReviews.ShouldBe(10);

        _spExecutorMock.Verify(e => e.QueryFirstAsync(
            It.IsAny<GetProductCategoryStatsProcedure>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStatsByIdAsync_WhenCategoryNotFound_ReturnsNull()
    {
        _spExecutorMock
            .Setup(e => e.QueryFirstAsync(
                It.IsAny<GetProductCategoryStatsProcedure>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductCategoryStats?)null);

        var result = await _sut.GetStatsByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    private static Category CreateCategory(string name, string? description = null)
    {
        return new Category
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = name,
            Description = description,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow }
        };
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid TenantId => TestTenantId;
        public bool HasTenant => true;
    }

    private sealed class TestActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }
}
