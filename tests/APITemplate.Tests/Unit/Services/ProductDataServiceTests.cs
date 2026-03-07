using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Application.Features.ProductData.Services;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Services;

public class ProductDataServiceTests
{
    private readonly Mock<IProductDataRepository> _repositoryMock;
    private readonly ProductDataService _sut;

    public ProductDataServiceTests()
    {
        _repositoryMock = new Mock<IProductDataRepository>();
        _sut = new ProductDataService(_repositoryMock.Object, TimeProvider.System);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var items = new List<ProductData>
        {
            new ImageProductData { Title = "Photo", Width = 1920, Height = 1080, Format = "jpg", FileSizeBytes = 500000 },
            new VideoProductData { Title = "Clip", DurationSeconds = 30, Resolution = "1080p", Format = "mp4", FileSizeBytes = 5000000 }
        };

        _repositoryMock
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _sut.GetAllAsync(ct: ct);

        result.Count.ShouldBe(2);
        result[0].Type.ShouldBe("image");
        result[1].Type.ShouldBe("video");
    }

    [Fact]
    public async Task GetAllAsync_WithTypeFilter_PassesTypeToRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.GetAllAsync("image", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ImageProductData { Title = "Photo", Width = 100, Height = 100, Format = "png", FileSizeBytes = 1000 }]);

        var result = await _sut.GetAllAsync("image", ct);

        result.Count.ShouldBe(1);
        result[0].Type.ShouldBe("image");
        _repositoryMock.Verify(r => r.GetAllAsync("image", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetAllAsync(ct: ct);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = new ImageProductData
        {
            Title = "Banner",
            Width = 800,
            Height = 600,
            Format = "jpg",
            FileSizeBytes = 200000
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(image.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(image);

        var result = await _sut.GetByIdAsync(image.Id, ct);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(image.Id);
        result.Type.ShouldBe("image");
        result.Title.ShouldBe("Banner");
        var imageResult = result.ShouldBeOfType<ImageProductDataResponse>();
        imageResult.Width.ShouldBe(800);
        imageResult.Height.ShouldBe(600);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData?)null);

        var result = await _sut.GetByIdAsync("nonexistent", ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateImageAsync_CreatesAndReturnsImageResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateImageProductDataRequest("Banner", "A banner", 1920, 1080, "jpg", 500000);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ImageProductData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData d, CancellationToken _) => d);

        var result = await _sut.CreateImageAsync(request, ct);

        result.ShouldNotBeNull();
        result.Type.ShouldBe("image");
        result.Title.ShouldBe("Banner");
        result.Description.ShouldBe("A banner");
        var imageResult = result.ShouldBeOfType<ImageProductDataResponse>();
        imageResult.Width.ShouldBe(1920);
        imageResult.Height.ShouldBe(1080);
        imageResult.Format.ShouldBe("jpg");
        imageResult.FileSizeBytes.ShouldBe(500000);

        _repositoryMock.Verify(
            r => r.CreateAsync(It.Is<ImageProductData>(e => e.Title == "Banner"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateVideoAsync_CreatesAndReturnsVideoResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateVideoProductDataRequest("Intro", null, 60, "1080p", "mp4", 10000000);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<VideoProductData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData d, CancellationToken _) => d);

        var result = await _sut.CreateVideoAsync(request, ct);

        result.ShouldNotBeNull();
        result.Type.ShouldBe("video");
        result.Title.ShouldBe("Intro");
        result.Description.ShouldBeNull();
        var videoResult = result.ShouldBeOfType<VideoProductDataResponse>();
        videoResult.DurationSeconds.ShouldBe(60);
        videoResult.Resolution.ShouldBe("1080p");
        videoResult.Format.ShouldBe("mp4");
        videoResult.FileSizeBytes.ShouldBe(10000000);
    }

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDelete()
    {
        var id = "507f1f77bcf86cd799439011";

        await _sut.DeleteAsync(id, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
