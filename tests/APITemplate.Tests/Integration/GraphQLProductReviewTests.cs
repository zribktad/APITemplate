using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class GraphQLProductReviewTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private Guid _userId;

    public GraphQLProductReviewTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphQL_CreateProductReview_ReturnsNewReview()
    {
        await AuthenticateAsync();
        var productId = await CreateProductViaGraphQLAsync("Review Target Product", 19.99m);

        var mutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) {
                        id
                        userId
                        rating
                        productId
                    }
                }",
            variables = new
            {
                input = new
                {
                    productId,
                    comment = "Tested via GraphQL",
                    rating = 4
                }
            }
        };

        var response = await PostGraphQLAsync(mutation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductReviewData>>(GraphQLJsonOptions.Default);
        result!.Data.CreateProductReview.UserId.ShouldBe(_userId);
        result.Data.CreateProductReview.Rating.ShouldBe(4);
        result.Data.CreateProductReview.ProductId.ShouldBe(productId);
    }

    [Fact]
    public async Task GraphQL_GetReviews_ReturnsEmptyOrPopulatedList()
    {
        await AuthenticateAsync();

        var query = new { query = "{ reviews { items { id userId rating } totalCount pageNumber pageSize } }" };

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ReviewsData>>(GraphQLJsonOptions.Default);
        result!.Data.Reviews.Items.Count.ShouldBeGreaterThanOrEqualTo(0);
        result.Data.Reviews.PageNumber.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GraphQL_GetReviewsByProductId_ReturnsReviewsForProduct()
    {
        await AuthenticateAsync();
        var productId = await CreateProductViaGraphQLAsync("Product With Reviews", 29.99m);

        var createMutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) { id }
                }",
            variables = new
            {
                input = new { productId, rating = 3 }
            }
        };
        await PostGraphQLAsync(createMutation);

        var query = new
        {
            query = $@"{{ reviewsByProductId(productId: ""{productId}"", pageNumber: 1, pageSize: 20) {{ items {{ id userId rating }} totalCount }} }}"
        };

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ReviewsByProductIdData>>(GraphQLJsonOptions.Default);
        result!.Data.ReviewsByProductId.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GraphQL_GetReviews_WithFilterSortAndPaging_ReturnsExpectedOrder()
    {
        await AuthenticateAsync();
        var productId = await CreateProductViaGraphQLAsync($"SortTarget-{Guid.NewGuid():N}", 15m);

        await CreateReviewViaGraphQLAsync(productId, 2);
        await CreateReviewViaGraphQLAsync(productId, 5);
        await CreateReviewViaGraphQLAsync(productId, 4);

        var query = new
        {
            query = @"
                query($input: ProductReviewQueryInput) {
                    reviews(input: $input) {
                        items { id userId rating productId }
                        totalCount
                        pageNumber
                        pageSize
                    }
                }",
            variables = new
            {
                input = new
                {
                    productId,
                    sortBy = "rating",
                    sortDirection = "desc",
                    pageNumber = 1,
                    pageSize = 2
                }
            }
        };

        var response = await PostGraphQLAsync(query);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ReviewsData>>(GraphQLJsonOptions.Default);
        var items = result!.Data.Reviews.Items;

        items.Count.ShouldBe(2);
        items[0].Rating.ShouldBeGreaterThanOrEqualTo(items[1].Rating);
        result.Data.Reviews.TotalCount.ShouldBeGreaterThanOrEqualTo(3);
        result.Data.Reviews.PageNumber.ShouldBe(1);
        result.Data.Reviews.PageSize.ShouldBe(2);
    }

    [Fact]
    public async Task GraphQL_DeleteProductReview_ReturnsTrue()
    {
        await AuthenticateAsync();
        var productId = await CreateProductViaGraphQLAsync("Product To Review Then Delete Review", 9.99m);

        var createMutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) { id }
                }",
            variables = new
            {
                input = new { productId, rating = 2 }
            }
        };

        var createResponse = await PostGraphQLAsync(createMutation);
        var createResult = await createResponse.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductReviewData>>(GraphQLJsonOptions.Default);
        var reviewId = createResult!.Data.CreateProductReview.Id;

        var deleteMutation = new
        {
            query = $@"mutation {{ deleteProductReview(id: ""{reviewId}"") }}"
        };

        var deleteResponse = await PostGraphQLAsync(deleteMutation);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<GraphQLResponse<DeleteProductReviewData>>(GraphQLJsonOptions.Default);
        deleteResult!.Data.DeleteProductReview.ShouldBeTrue();
    }

    private async Task AuthenticateAsync()
    {
        _userId = await IntegrationAuthHelper.AuthenticateAndGetUserIdAsync(_client, _factory.Services);
    }

    private async Task<Guid> CreateProductViaGraphQLAsync(string name, decimal price)
    {
        var mutation = new
        {
            query = @"
                mutation($input: CreateProductRequestInput!) {
                    createProduct(input: $input) { id }
                }",
            variables = new
            {
                input = new { name, price }
            }
        };

        var response = await PostGraphQLAsync(mutation);
        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductData>>(GraphQLJsonOptions.Default);
        return result!.Data.CreateProduct.Id;
    }

    private async Task<Guid> CreateReviewViaGraphQLAsync(Guid productId, int rating)
    {
        var mutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) { id }
                }",
            variables = new
            {
                input = new { productId, rating }
            }
        };

        var response = await PostGraphQLAsync(mutation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductReviewData>>(GraphQLJsonOptions.Default);
        return result!.Data.CreateProductReview.Id;
    }

    private async Task<HttpResponseMessage> PostGraphQLAsync(object query)
    {
        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content);
    }
}
