using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using APITemplate.Application.Features.ProductReview.Services;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Services;

public class ProductReviewServiceTests
{
    private readonly Mock<IProductReviewRepository> _reviewRepoMock;
    private readonly Mock<IProductReviewQueryService> _queryServiceMock;
    private readonly Mock<IProductRepository> _productRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ProductReviewService _sut;

    public ProductReviewServiceTests()
    {
        _reviewRepoMock = new Mock<IProductReviewRepository>();
        _queryServiceMock = new Mock<IProductReviewQueryService>();
        _productRepoMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());
        _sut = new ProductReviewService(
            _reviewRepoMock.Object,
            _queryServiceMock.Object,
            _productRepoMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllReviews()
    {
        var responses = new List<ProductReviewResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Alice", null, 5, DateTime.UtcNow),
            new(Guid.NewGuid(), Guid.NewGuid(), "Bob", null, 3, DateTime.UtcNow)
        };

        _queryServiceMock
            .Setup(q => q.GetPagedAsync(It.IsAny<ProductReviewFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ProductReviewResponse>(responses, 2, 1, 10));

        var result = await _sut.GetAllAsync(new ProductReviewFilter());

        result.Items.Count().ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenReviewExists_ReturnsResponse()
    {
        var review = new ProductReview
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ReviewerName = "Alice",
            Rating = 4,
            CreatedAt = DateTime.UtcNow
        };

        var response = new ProductReviewResponse(
            review.Id,
            review.ProductId,
            review.ReviewerName,
            review.Comment,
            review.Rating,
            review.CreatedAt);

        _queryServiceMock
            .Setup(q => q.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.GetByIdAsync(review.Id);

        result.ShouldNotBeNull();
        result!.ReviewerName.ShouldBe("Alice");
        result.Rating.ShouldBe(4);
    }

    [Fact]
    public async Task GetByIdAsync_WhenReviewDoesNotExist_ReturnsNull()
    {
        _queryServiceMock
            .Setup(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReviewResponse?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByProductIdAsync_ReturnsReviewsForProduct()
    {
        var productId = Guid.NewGuid();
        var responses = new List<ProductReviewResponse>
        {
            new(Guid.NewGuid(), productId, "Alice", null, 5, DateTime.UtcNow)
        };

        _queryServiceMock
            .Setup(q => q.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responses);

        var result = await _sut.GetByProductIdAsync(productId);

        result.Count.ShouldBe(1);
        result[0].ProductId.ShouldBe(productId);
    }

    [Fact]
    public async Task CreateAsync_WhenProductExists_CreatesReview()
    {
        var product = new Product { Id = Guid.NewGuid(), Name = "Test", Price = 10m, CreatedAt = DateTime.UtcNow };
        var request = new CreateProductReviewRequest(product.Id, "Alice", "Great!", 5);

        _productRepoMock
            .Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _reviewRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReview rv, CancellationToken _) => rv);

        var result = await _sut.CreateAsync(request);

        result.ReviewerName.ShouldBe("Alice");
        result.Rating.ShouldBe(5);
        result.ProductId.ShouldBe(product.Id);
        result.Id.ShouldNotBe(Guid.Empty);

        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenProductNotFound_ThrowsNotFoundException()
    {
        _productRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var request = new CreateProductReviewRequest(Guid.NewGuid(), "Alice", null, 3);

        var act = () => _sut.CreateAsync(request);

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDelete()
    {
        var id = Guid.NewGuid();

        await _sut.DeleteAsync(id);

        _reviewRepoMock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
