using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace APITemplate.Tests.Integration.Helpers;

internal sealed class GraphQLTestHelper
{
    private readonly HttpClient _client;

    internal GraphQLTestHelper(HttpClient client)
    {
        _client = client;
    }

    internal async Task<HttpResponseMessage> PostAsync(object query)
    {
        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content);
    }

    internal async Task<Guid> CreateProductAsync(string name, decimal price)
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

        var response = await PostAsync(mutation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductData>>(GraphQLJsonOptions.Default);
        return result!.Data.CreateProduct.Id;
    }

    internal async Task<Guid> CreateReviewAsync(Guid productId, int rating)
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

        var response = await PostAsync(mutation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductReviewData>>(GraphQLJsonOptions.Default);
        return result!.Data.CreateProductReview.Id;
    }
}
