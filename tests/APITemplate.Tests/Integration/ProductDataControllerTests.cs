using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Domain.Entities;
using APITemplate.Tests.Integration.Helpers;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class ProductDataControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IProductDataRepository> _repositoryMock;

    public ProductDataControllerTests(CustomWebApplicationFactory factory)
    {
        _repositoryMock = new Mock<IProductDataRepository>();

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<MongoDbContext>();
                services.RemoveAll<IProductDataRepository>();
                services.AddSingleton(_repositoryMock.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/product-data");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithToken_ReturnsOk()
    {
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _client.GetAsync("/api/v1/product-data");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithTypeFilter_PassesTypeToRepository()
    {
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.GetAllAsync("image", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ImageProductData { Title = "Photo", Width = 100, Height = 100, Format = "png", FileSizeBytes = 1000 }]);

        var response = await _client.GetAsync("/api/v1/product-data?type=image");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>(TestJsonOptions.CaseInsensitive);
        items.ShouldNotBeNull();
        items!.Length.ShouldBe(1);
        items[0].GetProperty("type").GetString().ShouldBe("image");
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsOk()
    {
        IntegrationAuthHelper.Authenticate(_client);

        var image = new ImageProductData { Title = "Banner", Width = 800, Height = 600, Format = "jpg", FileSizeBytes = 200000 };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(image.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(image);

        var response = await _client.GetAsync($"/api/v1/product-data/{image.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestJsonOptions.CaseInsensitive);
        json.GetProperty("title").GetString().ShouldBe("Banner");
        json.GetProperty("type").GetString().ShouldBe("image");
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData?)null);

        var response = await _client.GetAsync("/api/v1/product-data/507f1f77bcf86cd799439011");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateImage_ValidRequest_ReturnsCreated()
    {
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ImageProductData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData d, CancellationToken _) => d);

        var response = await _client.PostAsJsonAsync("/api/v1/product-data/image", new
        {
            Title = "Hero Banner",
            Description = "Main page hero",
            Width = 1920,
            Height = 1080,
            Format = "jpg",
            FileSizeBytes = 500000
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestJsonOptions.CaseInsensitive);
        json.GetProperty("type").GetString().ShouldBe("image");
        json.GetProperty("title").GetString().ShouldBe("Hero Banner");
        json.GetProperty("width").GetInt32().ShouldBe(1920);
    }

    [Fact]
    public async Task CreateImage_InvalidRequest_ReturnsBadRequest()
    {
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync("/api/v1/product-data/image", new
        {
            Title = "",
            Width = -1,
            Height = 0,
            Format = "bmp",
            FileSizeBytes = -100
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateVideo_ValidRequest_ReturnsCreated()
    {
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<VideoProductData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData d, CancellationToken _) => d);

        var response = await _client.PostAsJsonAsync("/api/v1/product-data/video", new
        {
            Title = "Product Demo",
            DurationSeconds = 120,
            Resolution = "1080p",
            Format = "mp4",
            FileSizeBytes = 10000000
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestJsonOptions.CaseInsensitive);
        json.GetProperty("type").GetString().ShouldBe("video");
        json.GetProperty("title").GetString().ShouldBe("Product Demo");
        json.GetProperty("durationSeconds").GetInt32().ShouldBe(120);
    }

    [Fact]
    public async Task CreateVideo_InvalidRequest_ReturnsBadRequest()
    {
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync("/api/v1/product-data/video", new
        {
            Title = "",
            DurationSeconds = 0,
            Resolution = "480p",
            Format = "wmv",
            FileSizeBytes = -1
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_WithToken_ReturnsNoContent()
    {
        IntegrationAuthHelper.Authenticate(_client);

        var id = "507f1f77bcf86cd799439011";

        _repositoryMock
            .Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync($"/api/v1/product-data/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _repositoryMock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}

