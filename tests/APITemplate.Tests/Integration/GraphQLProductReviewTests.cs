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

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductReviewData>>(GraphQLJsonOptions.Default);
        result!.Data.CreateProductReview.ReviewerName.ShouldBe("GraphQL Reviewer");
        result.Data.CreateProductReview.Rating.ShouldBe(4);
        result.Data.CreateProductReview.ProductId.ShouldBe(productId);
    }

    [Fact]
    public async Task GraphQL_GetReviews_ReturnsEmptyOrPopulatedList()
    {
        var query = new { query = "{ reviews { id reviewerName rating } }" };

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ReviewsData>>(GraphQLJsonOptions.Default);
        result!.Data.Reviews.Count.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GraphQL_GetReviewsByProductId_ReturnsReviewsForProduct()
    {
        var productId = await CreateProductViaGraphQLAsync("Product With Reviews", 29.99m);

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

        var query = new
        {
            query = $@"{{ reviewsByProductId(productId: ""{productId}"") {{ id reviewerName rating }} }}"
        };

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ReviewsByProductIdData>>(GraphQLJsonOptions.Default);
        result!.Data.ReviewsByProductId.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GraphQL_DeleteProductReview_ReturnsTrue()
    {
        var productId = await CreateProductViaGraphQLAsync("Product To Review Then Delete Review", 9.99m);

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

    private async Task<HttpResponseMessage> PostGraphQLAsync(object query)
    {
        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content);
    }
}
