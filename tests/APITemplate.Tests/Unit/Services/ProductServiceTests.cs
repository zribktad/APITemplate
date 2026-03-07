using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using APITemplate.Application.Features.Product.Specifications;
using APITemplate.Application.Features.Product.Services;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Services;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _repositoryMock;
    private readonly Mock<IProductQueryService> _queryServiceMock;
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly Mock<IProductDataRepository> _productDataRepositoryMock;
    private readonly Mock<IProductDataLinkRepository> _productDataLinkRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _repositoryMock = new Mock<IProductRepository>();
        _queryServiceMock = new Mock<IProductQueryService>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _productDataRepositoryMock = new Mock<IProductDataRepository>();
        _productDataLinkRepositoryMock = new Mock<IProductDataLinkRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _unitOfWorkMock.SetupImmediateTransactionExecution();
        _unitOfWorkMock.SetupImmediateTransactionExecution<Product>();
        _sut = new ProductService(
            _repositoryMock.Object,
            _queryServiceMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _productDataLinkRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetByIdAsync_ReturnsExpectedResult(bool productExists)
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = Guid.NewGuid();
        ProductResponse? response = null;
        if (productExists)
        {
            response = new ProductResponse(productId, "Test Product", "A test product", 9.99m, DateTime.UtcNow, []);
        }

        _queryServiceMock
            .Setup(q => q.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.GetByIdAsync(productId, ct);

        if (productExists)
        {
            result.ShouldNotBeNull();
            result!.Name.ShouldBe("Test Product");
            result.Price.ShouldBe(9.99m);
        }
        else
        {
            result.ShouldBeNull();
        }
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreatedProduct()
    {
        var request = new CreateProductRequest("New Product", "Description", 19.99m);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product p, CancellationToken _) => p);

        var result = await _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        result.Name.ShouldBe("New Product");
        result.Price.ShouldBe(19.99m);
        result.Id.ShouldNotBe(Guid.Empty);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<Product>>>(), It.IsAny<CancellationToken>(), It.IsAny<TransactionOptions?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithProductDataIds_NormalizesAndStoresUniqueLinks()
    {
        var productDataId = Guid.NewGuid();
        var request = new CreateProductRequest("New Product", "Description", 19.99m, null, [productDataId, productDataId]);

        _productDataRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ImageProductData { Id = productDataId, Title = "Image" }]);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product p, CancellationToken _) => p);

        var result = await _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        result.ProductDataIds.ShouldBe([productDataId]);
        _repositoryMock.Verify(
            r => r.AddAsync(It.Is<Product>(p => p.ProductDataLinks.Count == 1 && p.ProductDataLinks.Single().ProductDataId == productDataId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithCategoryId_ValidatesCategory()
    {
        var categoryId = Guid.NewGuid();
        var category = new Category { Id = categoryId, Name = "Test" };
        var request = new CreateProductRequest("New Product", "Description", 19.99m, categoryId);

        _categoryRepositoryMock
            .Setup(r => r.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product p, CancellationToken _) => p);

        var result = await _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        result.Name.ShouldBe("New Product");
        _categoryRepositoryMock.Verify(r => r.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentCategory_ThrowsNotFoundException()
    {
        var categoryId = Guid.NewGuid();
        var request = new CreateProductRequest("New Product", "Description", 19.99m, categoryId);

        _categoryRepositoryMock
            .Setup(r => r.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task CreateAsync_WithMissingProductData_ThrowsNotFoundException()
    {
        var productDataId = Guid.NewGuid();
        var request = new CreateProductRequest("New Product", "Description", 19.99m, null, [productDataId]);

        _productDataRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task UpdateAsync_WhenProductNotFound_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<ProductByIdWithLinksSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var act = () => _sut.UpdateAsync(Guid.NewGuid(), new UpdateProductRequest("Name", null, 10m), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDelete()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Delete me",
            Price = 10m,
            ProductDataLinks =
            [
                new ProductDataLink { ProductId = Guid.NewGuid(), ProductDataId = Guid.NewGuid() }
            ]
        };

        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<ProductByIdWithLinksSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productDataLinkRepositoryMock
            .Setup(r => r.ListByProductIdAsync(product.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.DeleteAsync(product.Id, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(
            r => r.DeleteAsync(product, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>(), It.IsAny<TransactionOptions?>()),
            Times.Once);
        product.ProductDataLinks.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WhenProductExists_UpdatesFields()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Description = "Old Desc",
            Price = 10m,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow },
            ProductDataLinks = []
        };

        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<ProductByIdWithLinksSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productDataLinkRepositoryMock
            .Setup(r => r.ListByProductIdAsync(product.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.UpdateAsync(product.Id, new UpdateProductRequest("New Name", "New Desc", 20m), TestContext.Current.CancellationToken);

        product.Name.ShouldBe("New Name");
        product.Description.ShouldBe("New Desc");
        product.Price.ShouldBe(20m);

        _repositoryMock.Verify(
            r => r.UpdateAsync(product, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>(), It.IsAny<TransactionOptions?>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesProductDataLinks()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Price = 10m,
            ProductDataLinks =
            [
                new ProductDataLink { ProductId = Guid.NewGuid(), ProductDataId = oldId }
            ]
        };
        var request = new UpdateProductRequest("New Name", "New Desc", 20m, null, [newId]);

        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<ProductByIdWithLinksSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productDataLinkRepositoryMock
            .Setup(r => r.ListByProductIdAsync(product.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product.ProductDataLinks.ToList());
        _productDataRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ImageProductData { Id = newId, Title = "Image" }]);

        await _sut.UpdateAsync(product.Id, request, TestContext.Current.CancellationToken);

        product.ProductDataLinks.Select(x => x.ProductDataId).ShouldBe([newId]);
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyProductDataIds_RemovesExistingLinks()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Price = 10m,
            ProductDataLinks =
            [
                new ProductDataLink { ProductId = Guid.NewGuid(), ProductDataId = Guid.NewGuid() }
            ]
        };

        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<ProductByIdWithLinksSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productDataLinkRepositoryMock
            .Setup(r => r.ListByProductIdAsync(product.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product.ProductDataLinks.ToList());

        await _sut.UpdateAsync(product.Id, new UpdateProductRequest("New Name", "New Desc", 20m, null, []), TestContext.Current.CancellationToken);

        product.ProductDataLinks.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_RestoresSoftDeletedProductDataLink()
    {
        var restoredId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Old Name",
            Price = 10m,
            ProductDataLinks = []
        };
        var deletedLink = ProductDataLink.Create(product.Id, restoredId, product.TenantId);
        deletedLink.IsDeleted = true;
        deletedLink.DeletedAtUtc = DateTime.UtcNow;
        deletedLink.DeletedBy = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<ProductByIdWithLinksSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productDataLinkRepositoryMock
            .Setup(r => r.ListByProductIdAsync(product.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([deletedLink]);
        _productDataRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ImageProductData { Id = restoredId, Title = "Image" }]);

        await _sut.UpdateAsync(product.Id, new UpdateProductRequest("New Name", null, 20m, null, [restoredId]), TestContext.Current.CancellationToken);

        product.ProductDataLinks.Select(x => x.ProductDataId).ShouldBe([restoredId]);
        product.ProductDataLinks.Single().IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProducts()
    {
        var ct = TestContext.Current.CancellationToken;
        var responses = new List<ProductResponse>
        {
            new(Guid.NewGuid(), "Product 1", null, 10m, DateTime.UtcNow, []),
            new(Guid.NewGuid(), "Product 2", null, 20m, DateTime.UtcNow, [])
        };

        _queryServiceMock
            .Setup(q => q.GetPagedAsync(It.IsAny<ProductFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ProductResponse>(responses, 2, 1, 10));

        var result = await _sut.GetAllAsync(new ProductFilter(), ct);

        result.Items.Count().ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }
}
