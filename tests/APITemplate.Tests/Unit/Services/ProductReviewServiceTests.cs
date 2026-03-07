using APITemplate.Application.Common.Context;
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
    private readonly Mock<IActorProvider> _actorProviderMock;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ProductReviewService _sut;

    public ProductReviewServiceTests()
    {
        _reviewRepoMock = new Mock<IProductReviewRepository>();
        _queryServiceMock = new Mock<IProductReviewQueryService>();
        _productRepoMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _actorProviderMock = new Mock<IActorProvider>();
        _actorProviderMock.Setup(a => a.ActorId).Returns(_currentUserId);
        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<ProductReview>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<ProductReview>> action, CancellationToken _) => action());
        _sut = new ProductReviewService(
            _reviewRepoMock.Object,
            _queryServiceMock.Object,
            _productRepoMock.Object,
            _unitOfWorkMock.Object,
            _actorProviderMock.Object);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllReviews()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var responses = new List<ProductReviewResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), userId, null, 5, DateTime.UtcNow),
            new(Guid.NewGuid(), Guid.NewGuid(), userId, null, 3, DateTime.UtcNow)
        };

        _queryServiceMock
            .Setup(q => q.GetPagedAsync(It.IsAny<ProductReviewFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ProductReviewResponse>(responses, 2, 1, 10));

        var result = await _sut.GetAllAsync(new ProductReviewFilter(), ct);

        result.Items.Count().ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetByIdAsync_ReturnsExpectedResult(bool reviewExists)
    {
        var ct = TestContext.Current.CancellationToken;
        var reviewId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        ProductReviewResponse? response = null;
        if (reviewExists)
        {
            response = new ProductReviewResponse(
                reviewId,
                Guid.NewGuid(),
                userId,
                null,
                4,
                DateTime.UtcNow);
        }

        _queryServiceMock
            .Setup(q => q.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.GetByIdAsync(reviewId, ct);

        if (reviewExists)
        {
            result.ShouldNotBeNull();
            result!.UserId.ShouldBe(userId);
            result.Rating.ShouldBe(4);
        }
        else
        {
            result.ShouldBeNull();
        }
    }

    [Fact]
    public async Task GetByProductIdAsync_ReturnsReviewsForProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var responses = new List<ProductReviewResponse>
        {
            new(Guid.NewGuid(), productId, userId, null, 5, DateTime.UtcNow)
        };

        _queryServiceMock
            .Setup(q => q.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responses);

        var result = await _sut.GetByProductIdAsync(productId, ct);

        result.Count.ShouldBe(1);
        result[0].ProductId.ShouldBe(productId);
    }

    [Fact]
    public async Task CreateAsync_WhenProductExists_CreatesReview()
    {
        var product = new Product { Id = Guid.NewGuid(), Name = "Test", Price = 10m, Audit = new() { CreatedAtUtc = DateTime.UtcNow } };
        var request = new CreateProductReviewRequest(product.Id, "Great!", 5);

        _productRepoMock
            .Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _reviewRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReview rv, CancellationToken _) => rv);

        var result = await _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        result.UserId.ShouldBe(_currentUserId);
        result.Rating.ShouldBe(5);
        result.ProductId.ShouldBe(product.Id);
        result.Id.ShouldNotBe(Guid.Empty);

        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenProductNotFound_ThrowsNotFoundException()
    {
        _productRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var request = new CreateProductReviewRequest(Guid.NewGuid(), null, 3);

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task DeleteAsync_WhenOwner_CallsRepositoryDelete()
    {
        var id = Guid.NewGuid();
        var review = new ProductReview { Id = id, UserId = _currentUserId, Rating = 3 };

        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        await _sut.DeleteAsync(id, TestContext.Current.CancellationToken);

        _reviewRepoMock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotOwner_ThrowsForbiddenException()
    {
        var id = Guid.NewGuid();
        var review = new ProductReview { Id = id, UserId = Guid.NewGuid(), Rating = 3 };

        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var act = () => _sut.DeleteAsync(id, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ForbiddenException>(act);
        _reviewRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();

        _reviewRepoMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReview?)null);

        var act = () => _sut.DeleteAsync(id, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<NotFoundException>(act);
    }
}
