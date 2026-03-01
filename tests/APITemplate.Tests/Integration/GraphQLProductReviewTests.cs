using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class GraphQLProductReviewTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GraphQLProductReviewTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphQL_CreateProductReview_ReturnsNewReview()
    {
        // Create product first via GraphQL
        var productId = await CreateProductViaGraphQLAsync("Review Target Product", 19.99m);

        var mutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) {
                        id
                        reviewerName
                        rating
                        productId
                    }
                }",
            variables = new
            {
                input = new
                {
                    productId,
                    reviewerName = "GraphQL Reviewer",
                    comment = "Tested via GraphQL",
                    rating = 4
                }
            }
        };

        var response = await PostGraphQLAsync(mutation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var review = json.GetProperty("data").GetProperty("createProductReview");
        review.GetProperty("reviewerName").GetString().ShouldBe("GraphQL Reviewer");
        review.GetProperty("rating").GetInt32().ShouldBe(4);
        review.GetProperty("productId").GetString().ShouldBe(productId.ToString());
    }

    [Fact]
    public async Task GraphQL_GetReviews_ReturnsEmptyOrPopulatedList()
    {
        var query = new { query = "{ reviews { id reviewerName rating } }" };

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var reviews = json.GetProperty("data").GetProperty("reviews");
        reviews.GetArrayLength().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GraphQL_GetReviewsByProductId_ReturnsReviewsForProduct()
    {
        var productId = await CreateProductViaGraphQLAsync("Product With Reviews", 29.99m);

        // Create a review
        var createMutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) { id }
                }",
            variables = new
            {
                input = new { productId, reviewerName = "Tester", rating = 3 }
            }
        };
        await PostGraphQLAsync(createMutation);

        // Query by product id
        var query = new
        {
            query = $@"{{ reviewsByProductId(productId: ""{productId}"") {{ id reviewerName rating }} }}"
        };

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var reviews = json.GetProperty("data").GetProperty("reviewsByProductId");
        reviews.GetArrayLength().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GraphQL_DeleteProductReview_ReturnsTrue()
    {
        var productId = await CreateProductViaGraphQLAsync("Product To Review Then Delete Review", 9.99m);

        // Create review
        var createMutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) { id }
                }",
            variables = new
            {
                input = new { productId, reviewerName = "ToDelete", rating = 2 }
            }
        };

        var createResponse = await PostGraphQLAsync(createMutation);
        var createJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var reviewId = createJson.GetProperty("data")
            .GetProperty("createProductReview")
            .GetProperty("id")
            .GetString();

        // Delete review
        var deleteMutation = new
        {
            query = $@"mutation {{ deleteProductReview(id: ""{reviewId}"") }}"
        };

        var deleteResponse = await PostGraphQLAsync(deleteMutation);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteJson = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        deleteJson.GetProperty("data")
            .GetProperty("deleteProductReview")
            .GetBoolean()
            .ShouldBeTrue();
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
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idStr = json.GetProperty("data")
            .GetProperty("createProduct")
            .GetProperty("id")
            .GetString()!;

        return Guid.Parse(idStr);
    }

    private async Task<HttpResponseMessage> PostGraphQLAsync(object query)
    {
        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content);
    }
}
