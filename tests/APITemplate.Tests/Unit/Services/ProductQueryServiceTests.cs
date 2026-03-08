using APITemplate.Application.Features.Product.Services;
using APITemplate.Application.Features.Product.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Services;

public sealed class ProductQueryServiceTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly ProductQueryService _sut;

    public ProductQueryServiceTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _sut = new ProductQueryService(_productRepositoryMock.Object);
    }

    [Fact]
    public async Task GetPagedAsync_ComposesPageAndFacetsFromReadRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        var filter = new ProductFilter(PageNumber: 2, PageSize: 20);
        IReadOnlyList<ProductResponse> items =
        [
            new ProductResponse(Guid.NewGuid(), "Product 1", null, 10m, DateTime.UtcNow, []),
            new ProductResponse(Guid.NewGuid(), "Product 2", null, 20m, DateTime.UtcNow, [])
        ];
        IReadOnlyList<ProductCategoryFacetValue> categoryFacets =
        [
            new ProductCategoryFacetValue(Guid.NewGuid(), "Audio", 2)
        ];
        IReadOnlyList<ProductPriceFacetBucketResponse> priceFacets =
        [
            new ProductPriceFacetBucketResponse("0 - 50", 0m, 50m, 2)
        ];

        _productRepositoryMock
            .Setup(repository => repository.ListAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        _productRepositoryMock
            .Setup(repository => repository.CountAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        _productRepositoryMock
            .Setup(repository => repository.GetCategoryFacetsAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categoryFacets);
        _productRepositoryMock
            .Setup(repository => repository.GetPriceFacetsAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceFacets);

        var result = await _sut.GetPagedAsync(filter, ct);

        result.Page.Items.ShouldBe(items);
        result.Page.TotalCount.ShouldBe(7);
        result.Page.PageNumber.ShouldBe(2);
        result.Page.PageSize.ShouldBe(20);
        result.Facets.Categories.ShouldBe(categoryFacets);
        result.Facets.PriceBuckets.ShouldBe(priceFacets);
    }

    [Fact]
    public async Task GetByIdAsync_UsesProductRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var expected = new ProductResponse(id, "Existing", null, 15m, DateTime.UtcNow, []);

        _productRepositoryMock
            .Setup(repository => repository.FirstOrDefaultAsync(It.IsAny<ProductByIdSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetByIdAsync(id, ct);

        result.ShouldBe(expected);
    }
}
