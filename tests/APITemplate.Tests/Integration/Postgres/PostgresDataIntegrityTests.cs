using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Domain.Entities;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

[Collection("Integration.Postgres")]
[Trait("Category", "Integration.Postgres")]
public sealed class PostgresDataIntegrityTests
{
    private readonly PostgresWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PostgresDataIntegrityTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RowVersion_OnUserInsert_IsGeneratedByApplication()
    {
        var username = $"rv-insert-{Guid.NewGuid():N}";
        var (_, user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "secret-pass");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persisted = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(u => u.Id == user.Id);

        persisted.RowVersion.ShouldNotBeNull();
        persisted.RowVersion.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RowVersion_OnUserUpdate_ChangesAfterUpdate()
    {
        var username = $"rv-update-{Guid.NewGuid():N}";
        var (_, user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "secret-pass");

        byte[] before;

        await using (var beforeScope = _factory.Services.CreateAsyncScope())
        {
            var db = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.Users
                .IgnoreQueryFilters()
                .SingleAsync(u => u.Id == user.Id);

            before = entity.RowVersion.ToArray();
            entity.Email = $"{username}.updated@example.com";
            await db.SaveChangesAsync();
        }

        await using var afterScope = _factory.Services.CreateAsyncScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = await afterDb.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(u => u.Id == user.Id);

        after.RowVersion.ShouldNotBeNull();
        after.RowVersion.Length.ShouldBeGreaterThan(0);
        after.RowVersion.SequenceEqual(before).ShouldBeFalse();
    }

    [Fact]
    public async Task CategoryStats_ReturnsTenantScopedAndSoftDeleteScopedValues()
    {
        var usernameA = $"tenant-a-{Guid.NewGuid():N}";
        var usernameB = $"tenant-b-{Guid.NewGuid():N}";

        var (tenantA, userA) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            usernameA,
            $"{usernameA}@example.com",
            "pass-a");

        var (tenantB, userB) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            usernameB,
            $"{usernameB}@example.com",
            "pass-b");

        Guid categoryAId;
        Guid categoryBId;
        Guid productAId;
        Guid reviewToSoftDeleteId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var categoryA = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                Name = $"Category-A-{Guid.NewGuid():N}"
            };

            var categoryB = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB.Id,
                Name = $"Category-B-{Guid.NewGuid():N}"
            };

            var productA1 = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                Name = $"Product-A1-{Guid.NewGuid():N}",
                Price = 100m,
                CategoryId = categoryA.Id
            };

            var productA2 = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                Name = $"Product-A2-{Guid.NewGuid():N}",
                Price = 300m,
                CategoryId = categoryA.Id
            };

            var productB1 = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB.Id,
                Name = $"Product-B1-{Guid.NewGuid():N}",
                Price = 999m,
                CategoryId = categoryB.Id
            };

            var reviewA1 = new ProductReview
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                ProductId = productA1.Id,
                UserId = userA.Id,
                Rating = 5
            };

            var reviewA2 = new ProductReview
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA.Id,
                ProductId = productA1.Id,
                UserId = userA.Id,
                Rating = 4
            };

            var reviewB1 = new ProductReview
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB.Id,
                ProductId = productB1.Id,
                UserId = userB.Id,
                Rating = 3
            };

            db.Categories.AddRange(categoryA, categoryB);
            db.Products.AddRange(productA1, productA2, productB1);
            db.ProductReviews.AddRange(reviewA1, reviewA2, reviewB1);
            await db.SaveChangesAsync();

            // Mark one review and one product as soft-deleted to verify SQL function filtering.
            reviewA2.IsDeleted = true;
            reviewA2.DeletedAtUtc = DateTime.UtcNow;
            reviewA2.DeletedBy = "test";

            productA2.IsDeleted = true;
            productA2.DeletedAtUtc = DateTime.UtcNow;
            productA2.DeletedBy = "test";
            await db.SaveChangesAsync();

            categoryAId = categoryA.Id;
            categoryBId = categoryB.Id;
            productAId = productA1.Id;
            reviewToSoftDeleteId = reviewA2.Id;
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenantA.Id, username: usernameA, role: Domain.Enums.UserRole.TenantUser);

        var statsResponse = await _client.GetAsync($"/api/v1/categories/{categoryAId}/stats");
        statsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await statsResponse.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("categoryId").GetGuid().ShouldBe(categoryAId);
        payload.GetProperty("productCount").GetInt64().ShouldBe(1);
        payload.GetProperty("averagePrice").GetDecimal().ShouldBe(100m);
        payload.GetProperty("totalReviews").GetInt64().ShouldBe(1);

        // Tenant A token must not access stats of tenant B category.
        var forbiddenByIsolation = await _client.GetAsync($"/api/v1/categories/{categoryBId}/stats");
        forbiddenByIsolation.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Ensure soft-deleted review is hidden from tenant-scoped query path.
        var reviewById = await _client.GetAsync($"/api/v1/productreviews/{reviewToSoftDeleteId}");
        reviewById.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var reviewsByProduct = await _client.GetAsync($"/api/v1/productreviews/by-product/{productAId}");
        reviewsByProduct.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reviews = await reviewsByProduct.Content.ReadFromJsonAsync<JsonElement[]>();
        reviews.ShouldNotBeNull();
        reviews!.Length.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteProduct_WithExistingReviews_SoftDeletesReviews_Cascade()
    {
        var username = $"cascade-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "pass-cascade");

        Guid productId;
        Guid review1Id;
        Guid review2Id;
        Guid categoryId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var category = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = $"Category-Cascade-{Guid.NewGuid():N}"
            };

            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        var cascadeUserId = IntegrationAuthHelper.AuthenticateAndGetUserId(_client, tenantId: tenant.Id, username: username, role: Domain.Enums.UserRole.TenantUser);

        var createProductResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Name = $"Product-Cascade-{Guid.NewGuid():N}",
                Price = 88m,
                CategoryId = categoryId
            });
        createProductResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdProduct = await createProductResponse.Content.ReadFromJsonAsync<JsonElement>();
        productId = createdProduct.GetProperty("id").GetGuid();

        var createReview1 = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = productId, UserId = cascadeUserId, Rating = 5 });
        createReview1.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdReview1 = await createReview1.Content.ReadFromJsonAsync<JsonElement>();
        review1Id = createdReview1.GetProperty("id").GetGuid();

        var createReview2 = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = productId, UserId = cascadeUserId, Rating = 4 });
        createReview2.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdReview2 = await createReview2.Content.ReadFromJsonAsync<JsonElement>();
        review2Id = createdReview2.GetProperty("id").GetGuid();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/products/{productId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reviewsResponse = await _client.GetAsync($"/api/v1/productreviews/by-product/{productId}");
        reviewsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var visibleReviews = await reviewsResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        visibleReviews.ShouldNotBeNull();
        visibleReviews.ShouldBeEmpty();

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var allReviews = await verifyDb.ProductReviews
            .IgnoreQueryFilters()
            .Where(r => r.Id == review1Id || r.Id == review2Id)
            .ToListAsync();

        allReviews.Count.ShouldBe(2);
        allReviews.All(r => r.IsDeleted).ShouldBeTrue();
    }

    [Fact]
    public async Task CategoryStats_FunctionCallable_ReturnsZeroValuesForEmptyCategory()
    {
        var username = $"stats-smoke-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "pass-smoke");

        Guid categoryId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = $"Category-Stats-Smoke-{Guid.NewGuid():N}"
            };

            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenant.Id, username: username, role: Domain.Enums.UserRole.TenantUser);

        var response = await _client.GetAsync($"/api/v1/categories/{categoryId}/stats");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("categoryId").GetGuid().ShouldBe(categoryId);
        payload.GetProperty("productCount").GetInt64().ShouldBe(0);
        payload.GetProperty("averagePrice").GetDecimal().ShouldBe(0m);
        payload.GetProperty("totalReviews").GetInt64().ShouldBe(0);
    }
}
