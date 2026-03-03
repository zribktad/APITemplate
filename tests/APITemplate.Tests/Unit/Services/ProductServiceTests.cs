using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using APITemplate.Application.Features.Product.Services;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Services;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _repositoryMock;
    private readonly Mock<IProductQueryService> _queryServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _repositoryMock = new Mock<IProductRepository>();
        _queryServiceMock = new Mock<IProductQueryService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sut = new ProductService(_repositoryMock.Object, _queryServiceMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductExists_ReturnsProductResponse()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Description = "A test product",
            Price = 9.99m,
            CreatedAt = DateTime.UtcNow
        };

        var response = new ProductResponse(product.Id, product.Name, product.Description, product.Price, product.CreatedAt);
        _queryServiceMock
            .Setup(q => q.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.GetByIdAsync(product.Id);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("Test Product");
        result.Price.ShouldBe(9.99m);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductDoesNotExist_ReturnsNull()
    {
        _queryServiceMock
            .Setup(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponse?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreatedProduct()
    {
        var request = new CreateProductRequest("New Product", "Description", 19.99m);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product p, CancellationToken _) => p);

        var result = await _sut.CreateAsync(request);

        result.Name.ShouldBe("New Product");
        result.Price.ShouldBe(19.99m);
        result.Id.ShouldNotBe(Guid.Empty);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenProductNotFound_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var act = () => _sut.UpdateAsync(Guid.NewGuid(), new UpdateProductRequest("Name", null, 10m));

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDelete()
    {
        var id = Guid.NewGuid();

        await _sut.DeleteAsync(id);

        _repositoryMock.Verify(
            r => r.DeleteAsync(id, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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
            CreatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        await _sut.UpdateAsync(product.Id, new UpdateProductRequest("New Name", "New Desc", 20m));

        product.Name.ShouldBe("New Name");
        product.Description.ShouldBe("New Desc");
        product.Price.ShouldBe(20m);

        _repositoryMock.Verify(
            r => r.UpdateAsync(product, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProducts()
    {
        var responses = new List<ProductResponse>
        {
            new(Guid.NewGuid(), "Product 1", null, 10m, DateTime.UtcNow),
            new(Guid.NewGuid(), "Product 2", null, 20m, DateTime.UtcNow)
        };

        _queryServiceMock
            .Setup(q => q.GetPagedAsync(It.IsAny<ProductFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ProductResponse>(responses, 2, 1, 10));

        var result = await _sut.GetAllAsync(new ProductFilter());

        result.Items.Count().ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }
}
