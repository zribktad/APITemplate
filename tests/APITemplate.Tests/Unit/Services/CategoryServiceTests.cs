using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Services;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CategoryService _sut;

    public CategoryServiceTests()
    {
        _repositoryMock = new Mock<ICategoryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sut = new CategoryService(_repositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllCategories()
    {
        var categories = new List<Category>
        {
            new() { Id = Guid.NewGuid(), Name = "Electronics", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Books", Description = "All books", CreatedAt = DateTime.UtcNow }
        };

        _repositoryMock
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        var result = await _sut.GetAllAsync();

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("Electronics");
        result[1].Name.ShouldBe("Books");
        result[1].Description.ShouldBe("All books");
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        _repositoryMock
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetAllAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenCategoryExists_ReturnsResponse()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Electronics",
            Description = "Electronic devices",
            CreatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var result = await _sut.GetByIdAsync(category.Id);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(category.Id);
        result.Name.ShouldBe("Electronics");
        result.Description.ShouldBe("Electronic devices");
    }

    [Fact]
    public async Task GetByIdAsync_WhenCategoryDoesNotExist_ReturnsNull()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_CreatesAndReturnsCategoryResponse()
    {
        var request = new CreateCategoryRequest("Electronics", "Electronic devices");

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c, CancellationToken _) => c);

        var result = await _sut.CreateAsync(request);

        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe("Electronics");
        result.Description.ShouldBe("Electronic devices");

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNullDescription_CreatesCategory()
    {
        var request = new CreateCategoryRequest("Books", null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c, CancellationToken _) => c);

        var result = await _sut.CreateAsync(request);

        result.Name.ShouldBe("Books");
        result.Description.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenCategoryExists_UpdatesAndCommits()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Description = "Old Description",
            CreatedAt = DateTime.UtcNow
        };

        var request = new UpdateCategoryRequest("New Name", "New Description");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        await _sut.UpdateAsync(category.Id, request);

        _repositoryMock.Verify(r => r.UpdateAsync(
            It.Is<Category>(c => c.Name == "New Name" && c.Description == "New Description"),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenCategoryDoesNotExist_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var act = () => _sut.UpdateAsync(Guid.NewGuid(), new UpdateCategoryRequest("Name", null));

        await Should.ThrowAsync<NotFoundException>(act);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDeleteAndCommits()
    {
        var id = Guid.NewGuid();

        await _sut.DeleteAsync(id);

        _repositoryMock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStatsAsync_WhenStatsExist_ReturnsMappedResponse()
    {
        var categoryId = Guid.NewGuid();
        var stats = new ProductCategoryStats
        {
            CategoryId = categoryId,
            CategoryName = "Electronics",
            ProductCount = 5,
            AveragePrice = 199.99m,
            TotalReviews = 42
        };

        _repositoryMock
            .Setup(r => r.GetStatsByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var result = await _sut.GetStatsAsync(categoryId);

        result.ShouldNotBeNull();
        result!.CategoryId.ShouldBe(categoryId);
        result.CategoryName.ShouldBe("Electronics");
        result.ProductCount.ShouldBe(5);
        result.AveragePrice.ShouldBe(199.99m);
        result.TotalReviews.ShouldBe(42);
    }

    [Fact]
    public async Task GetStatsAsync_WhenCategoryDoesNotExist_ReturnsNull()
    {
        _repositoryMock
            .Setup(r => r.GetStatsByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductCategoryStats?)null);

        var result = await _sut.GetStatsAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }
}
