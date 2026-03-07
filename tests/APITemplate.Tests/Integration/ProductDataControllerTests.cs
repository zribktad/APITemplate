using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Domain.Entities;
using APITemplate.Tests.Integration.Helpers;
using APITemplate.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class ProductDataControllerTests
{
    private readonly HttpClient _client;
    private readonly Mock<IProductDataRepository> _repositoryMock;

    public ProductDataControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _repositoryMock = factory.Services.GetRequiredService<Mock<IProductDataRepository>>();
        _repositoryMock.Reset();
    }

    [Fact]
    public async Task GetAll_WithToken_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _client.GetAsync("/api/v1/product-data", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithTypeFilter_PassesTypeToRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.GetAllAsync("image", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ImageProductData { Title = "Photo", Width = 100, Height = 100, Format = "png", FileSizeBytes = 1000 }]);

        var response = await _client.GetAsync("/api/v1/product-data?type=image", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>(TestJsonOptions.CaseInsensitive, ct);
        items.ShouldNotBeNull();
        items!.Length.ShouldBe(1);
        items[0].GetProperty("type").GetString().ShouldBe("image");
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var image = new ImageProductData { Title = "Banner", Width = 800, Height = 600, Format = "jpg", FileSizeBytes = 200000 };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(image.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(image);

        var response = await _client.GetAsync($"/api/v1/product-data/{image.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestJsonOptions.CaseInsensitive, ct);
        json.GetProperty("title").GetString().ShouldBe("Banner");
        json.GetProperty("type").GetString().ShouldBe("image");
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData?)null);

        var response = await _client.GetAsync("/api/v1/product-data/507f1f77bcf86cd799439011", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("image")]
    [InlineData("video")]
    public async Task Create_ValidRequest_ReturnsCreated(string type)
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        if (type == "image")
        {
            _repositoryMock
                .Setup(r => r.CreateAsync(It.IsAny<ImageProductData>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductData d, CancellationToken _) => d);
        }
        else
        {
            _repositoryMock
                .Setup(r => r.CreateAsync(It.IsAny<VideoProductData>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductData d, CancellationToken _) => d);
        }

        object payload = type == "image"
            ? new { Title = "Hero Banner", Description = "Main page hero", Width = 1920, Height = 1080, Format = "jpg", FileSizeBytes = 500000 }
            : new { Title = "Product Demo", DurationSeconds = 120, Resolution = "1080p", Format = "mp4", FileSizeBytes = 10000000 };

        var response = await _client.PostAsJsonAsync($"/api/v1/product-data/{type}", payload, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestJsonOptions.CaseInsensitive, ct);
        json.GetProperty("type").GetString().ShouldBe(type);
    }

    [Theory]
    [InlineData("image")]
    [InlineData("video")]
    public async Task Create_InvalidRequest_ReturnsBadRequest(string type)
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        object payload = type == "image"
            ? new { Title = "", Width = -1, Height = 0, Format = "bmp", FileSizeBytes = -100 }
            : new { Title = "", DurationSeconds = 0, Resolution = "480p", Format = "wmv", FileSizeBytes = -1 };

        var response = await _client.PostAsJsonAsync($"/api/v1/product-data/{type}", payload, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_WithToken_ReturnsNoContent()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var id = "507f1f77bcf86cd799439011";

        _repositoryMock
            .Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync($"/api/v1/product-data/{id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _repositoryMock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
