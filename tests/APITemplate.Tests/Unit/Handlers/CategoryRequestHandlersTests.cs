using APITemplate.Application.Features.Category;
using APITemplate.Application.Features.Category.Specifications;
using APITemplate.Application.Features.Category.Mappings;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public class CategoryRequestHandlersTests
{
    private readonly Mock<ICategoryRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CategoryRequestHandlers _sut;

    public CategoryRequestHandlersTests()
    {
        _repositoryMock = new Mock<ICategoryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _unitOfWorkMock.SetupImmediateTransactionExecution();
        _unitOfWorkMock.SetupImmediateTransactionExecution<Category>();
        _sut = new CategoryRequestHandlers(_repositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPagedCategories()
    {
        var ct = TestContext.Current.CancellationToken;
        var items = new List<CategoryResponse>
        {
            new(Guid.NewGuid(), "Electronics", null, DateTime.UtcNow),
            new(Guid.NewGuid(), "Books", "All books", DateTime.UtcNow)
        };

        _repositoryMock
            .Setup(r => r.ListAsync(It.IsAny<CategorySpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        _repositoryMock
            .Setup(r => r.CountAsync(It.IsAny<CategoryCountSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _sut.Handle(new GetCategoriesQuery(new CategoryFilter()), ct);

        result.Items.Count().ShouldBe(2);
        result.Items.First().Name.ShouldBe("Electronics");
        result.Items.Last().Name.ShouldBe("Books");
        result.Items.Last().Description.ShouldBe("All books");
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.ListAsync(It.IsAny<CategorySpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CategoryResponse>());
        _repositoryMock
            .Setup(r => r.CountAsync(It.IsAny<CategoryCountSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.Handle(new GetCategoriesQuery(new CategoryFilter()), ct);

        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenCategoryExists_ReturnsResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoryId = Guid.NewGuid();
        var response = new CategoryResponse(categoryId, "Electronics", "Electronic devices", DateTime.UtcNow);

        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<CategoryByIdSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.Handle(new GetCategoryByIdQuery(categoryId), ct);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(categoryId);
        result.Name.ShouldBe("Electronics");
        result.Description.ShouldBe("Electronic devices");
    }

    [Fact]
    public async Task GetByIdAsync_WhenCategoryDoesNotExist_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<CategoryByIdSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CategoryResponse?)null);

        var result = await _sut.Handle(new GetCategoryByIdQuery(Guid.NewGuid()), ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_CreatesAndReturnsCategoryResponse()
    {
        var request = new CreateCategoryRequest("Electronics", "Electronic devices");

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c, CancellationToken _) => c);

        var result = await _sut.Handle(new CreateCategoryCommand(request), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe("Electronics");
        result.Description.ShouldBe("Electronic devices");

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Category>>>(), It.IsAny<CancellationToken>(), It.IsAny<TransactionOptions?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNullDescription_CreatesCategory()
    {
        var request = new CreateCategoryRequest("Books", null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c, CancellationToken _) => c);

        var result = await _sut.Handle(new CreateCategoryCommand(request), TestContext.Current.CancellationToken);

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
            Audit = new() { CreatedAtUtc = DateTime.UtcNow }
        };

        var request = new UpdateCategoryRequest("New Name", "New Description");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        await _sut.Handle(new UpdateCategoryCommand(category.Id, request), TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.UpdateAsync(
            It.Is<Category>(c => c.Name == "New Name" && c.Description == "New Description"),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>(), It.IsAny<TransactionOptions?>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenCategoryDoesNotExist_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var act = () => _sut.Handle(new UpdateCategoryCommand(Guid.NewGuid(), new UpdateCategoryRequest("Name", null)), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<NotFoundException>(act);
        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>(), It.IsAny<TransactionOptions?>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDeleteAndCommits()
    {
        var id = Guid.NewGuid();

        await _sut.Handle(new DeleteCategoryCommand(id), TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>(), It.IsAny<TransactionOptions?>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStatsAsync_WhenStatsExist_ReturnsMappedResponse()
    {
        var ct = TestContext.Current.CancellationToken;
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

        var result = await _sut.Handle(new GetCategoryStatsQuery(categoryId), ct);

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
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.GetStatsByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductCategoryStats?)null);

        var result = await _sut.Handle(new GetCategoryStatsQuery(Guid.NewGuid()), ct);

        result.ShouldBeNull();
    }
}
