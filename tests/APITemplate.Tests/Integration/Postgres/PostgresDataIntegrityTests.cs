using System.Net;
using System.Net.Http.Json;
using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Features.Product;
using APITemplate.Application.Features.Product.Repositories;
using APITemplate.Application.Features.ProductReview;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using APITemplate.Extensions;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Tests.Integration.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
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
    public async Task ProductSearch_FullTextAndFacets_ReturnTenantScopedResults()
    {
        var ct = TestContext.Current.CancellationToken;
        var username = $"search-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "pass-search",
            ct: ct);

        var otherTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = $"search-other-{Guid.NewGuid():N}",
            Name = "Other Search Tenant"
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var electronics = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Electronics"
            };

            var books = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Books"
            };

            var otherCategory = new Category
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenant.Id,
                Name = "Electronics"
            };

            db.Tenants.Add(otherTenant);
            db.Categories.AddRange(electronics, books, otherCategory);
            db.Products.AddRange(
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Wireless Mouse",
                    Description = "Silent office mouse",
                    Price = 30m,
                    CategoryId = electronics.Id
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Wireless Keyboard",
                    Description = "Mechanical office keyboard",
                    Price = 80m,
                    CategoryId = electronics.Id
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Fantasy Novel",
                    Description = "Epic dragon story",
                    Price = 15m,
                    CategoryId = books.Id
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = otherTenant.Id,
                    Name = "Wireless Speaker",
                    Description = "Other tenant item",
                    Price = 120m,
                    CategoryId = otherCategory.Id
                });

            await db.SaveChangesAsync(ct);
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenant.Id, username: username, role: Domain.Enums.UserRole.User);

        var response = await _client.GetAsync("/api/v1/products?query=wireless", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(TestJsonOptions.CaseInsensitive, ct);
        payload.ShouldNotBeNull();

        payload!.Page.Items.Count().ShouldBe(2);
        payload.Page.Items.Select(item => item.Name).ShouldBe(["Wireless Mouse", "Wireless Keyboard"], ignoreOrder: true);
        payload.Facets.Categories.Count.ShouldBe(1);
        payload.Facets.Categories.Single().CategoryName.ShouldBe("Electronics");
        payload.Facets.Categories.Single().Count.ShouldBe(2);
        payload.Facets.PriceBuckets.Single(bucket => bucket.Label == "0 - 50").Count.ShouldBe(1);
        payload.Facets.PriceBuckets.Single(bucket => bucket.Label == "50 - 100").Count.ShouldBe(1);
    }

    [Fact]
    public async Task GraphQL_SearchQueries_UsePostgresFullText()
    {
        var ct = TestContext.Current.CancellationToken;
        var graphql = new GraphQLTestHelper(_client);
        var username = $"graphql-search-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "pass-graphql-search",
            ct: ct);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Categories.AddRange(
                new Category
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Office Supplies",
                    Description = "Desk organization"
                },
                new Category
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Kitchen Goods",
                    Description = "Cookware"
                });

            db.Products.AddRange(
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Wireless Charger",
                    Description = "Fast charging pad",
                    Price = 40m
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Paper Notebook",
                    Description = "Meeting notes",
                    Price = 12m
                });

            await db.SaveChangesAsync(ct);
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenant.Id, username: username, role: Domain.Enums.UserRole.User);

        var productsQuery = new
        {
            query = @"
                query($input: ProductQueryInput) {
                    products(input: $input) {
                        page {
                            items { id name price }
                            totalCount
                        }
                        facets { priceBuckets { label count } }
                    }
                }",
            variables = new
            {
                input = new
                {
                    query = "wireless",
                    pageNumber = 1,
                    pageSize = 10
                }
            }
        };

        var products = await graphql.ReadRequiredGraphQLFieldAsync<ProductsData, ProductPage>(
            await graphql.PostAsync(productsQuery),
            data => data.Products,
            "products");

        products.Page.Items.Count.ShouldBe(1);
        products.Page.Items[0].Name.ShouldBe("Wireless Charger");

        var categoriesQuery = new
        {
            query = @"
                query($input: CategoryQueryInput) {
                    categories(input: $input) {
                        page {
                            items { id name }
                            totalCount
                        }
                    }
                }",
            variables = new
            {
                input = new
                {
                    query = "office",
                    pageNumber = 1,
                    pageSize = 10
                }
            }
        };

        var categories = await graphql.ReadRequiredGraphQLFieldAsync<CategoriesData, CategoryPage>(
            await graphql.PostAsync(categoriesQuery),
            data => data.Categories,
            "categories");

        categories.Page.Items.Count.ShouldBe(1);
        categories.Page.Items[0].Name.ShouldBe("Office Supplies");
        categories.Page.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task GlobalQueryFilters_IsolateProductsAndReviewsAcrossTenants()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantA = new Tenant { Id = Guid.NewGuid(), Code = $"tenant-a-{Guid.NewGuid():N}", Name = "Tenant A" };
        var tenantB = new Tenant { Id = Guid.NewGuid(), Code = $"tenant-b-{Guid.NewGuid():N}", Name = "Tenant B" };
        var categoryA = new Category { Id = Guid.NewGuid(), TenantId = tenantA.Id, Name = $"Category-A-{Guid.NewGuid():N}" };
        var categoryB = new Category { Id = Guid.NewGuid(), TenantId = tenantB.Id, Name = $"Category-B-{Guid.NewGuid():N}" };
        var userA = new AppUser { Id = Guid.NewGuid(), TenantId = tenantA.Id, Username = $"usera-{Guid.NewGuid():N}", Email = $"a-{Guid.NewGuid():N}@example.com", PasswordHash = "hash" };
        var userB = new AppUser { Id = Guid.NewGuid(), TenantId = tenantB.Id, Username = $"userb-{Guid.NewGuid():N}", Email = $"b-{Guid.NewGuid():N}@example.com", PasswordHash = "hash" };
        var productA = new Product { Id = Guid.NewGuid(), TenantId = tenantA.Id, Name = $"Product-A-{Guid.NewGuid():N}", Price = 10m, CategoryId = categoryA.Id };
        var productB = new Product { Id = Guid.NewGuid(), TenantId = tenantB.Id, Name = $"Product-B-{Guid.NewGuid():N}", Price = 20m, CategoryId = categoryB.Id };
        var reviewA = new ProductReview { Id = Guid.NewGuid(), TenantId = tenantA.Id, ProductId = productA.Id, UserId = userA.Id, Rating = 5 };
        var reviewB = new ProductReview { Id = Guid.NewGuid(), TenantId = tenantB.Id, ProductId = productB.Id, UserId = userB.Id, Rating = 4 };

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.AddRange(tenantA, tenantB);
            seedContext.Users.AddRange(userA, userB);
            seedContext.Categories.AddRange(categoryA, categoryB);
            seedContext.Products.AddRange(productA, productB);
            seedContext.ProductReviews.AddRange(reviewA, reviewB);
            await seedContext.SaveChangesAsync(ct);
        }

        await using var tenantAContext = await CreateDbContextAsync(true, tenantA.Id, actorId, ct);
        await using var tenantBContext = await CreateDbContextAsync(true, tenantB.Id, actorId, ct);
        await using var unrestrictedContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);

        var tenantAProducts = await tenantAContext.Products.OrderBy(p => p.Id).ToListAsync(ct);
        var tenantAReviews = await tenantAContext.ProductReviews.OrderBy(r => r.Id).ToListAsync(ct);
        var tenantBProducts = await tenantBContext.Products.OrderBy(p => p.Id).ToListAsync(ct);
        var tenantBReviews = await tenantBContext.ProductReviews.OrderBy(r => r.Id).ToListAsync(ct);
        var allProducts = await unrestrictedContext.Products
            .IgnoreQueryFilters()
            .Where(p => p.Id == productA.Id || p.Id == productB.Id)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);
        var allReviews = await unrestrictedContext.ProductReviews
            .IgnoreQueryFilters()
            .Where(r => r.Id == reviewA.Id || r.Id == reviewB.Id)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        tenantAProducts.Select(p => p.Id).ShouldBe([productA.Id]);
        tenantAReviews.Select(r => r.Id).ShouldBe([reviewA.Id]);
        tenantBProducts.Select(p => p.Id).ShouldBe([productB.Id]);
        tenantBReviews.Select(r => r.Id).ShouldBe([reviewB.Id]);
        allProducts.Select(p => p.Id).ShouldBe([productA.Id, productB.Id], ignoreOrder: true);
        allReviews.Select(r => r.Id).ShouldBe([reviewA.Id, reviewB.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task XminConcurrency_ConcurrentUpdate_ThrowsDbUpdateConcurrencyException()
    {
        var ct = TestContext.Current.CancellationToken;
        var username = $"xmin-test-{Guid.NewGuid():N}";
        var (_, user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "secret-pass",
            ct: ct);

        await using var scope1 = _factory.Services.CreateAsyncScope();
        await using var scope2 = _factory.Services.CreateAsyncScope();

        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity1 = await db1.Users
            .IgnoreQueryFilters()
            .SingleAsync(u => u.Id == user.Id, ct);

        var entity2 = await db2.Users
            .IgnoreQueryFilters()
            .SingleAsync(u => u.Id == user.Id, ct);

        entity1.Email = $"{username}.first@example.com";
        await db1.SaveChangesAsync(ct);

        entity2.Email = $"{username}.second@example.com";
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task CategoryStats_ReturnsTenantScopedAndSoftDeleteScopedValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var usernameA = $"tenant-a-{Guid.NewGuid():N}";
        var usernameB = $"tenant-b-{Guid.NewGuid():N}";

        var (tenantA, userA) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            usernameA,
            $"{usernameA}@example.com",
            "pass-a",
            ct: ct);

        var (tenantB, userB) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            usernameB,
            $"{usernameB}@example.com",
            "pass-b",
            ct: ct);

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
            await db.SaveChangesAsync(ct);

            // Mark one review and one product as soft-deleted to verify SQL function filtering.
            reviewA2.IsDeleted = true;
            reviewA2.DeletedAtUtc = DateTime.UtcNow;
            reviewA2.DeletedBy = Guid.NewGuid();

            productA2.IsDeleted = true;
            productA2.DeletedAtUtc = DateTime.UtcNow;
            productA2.DeletedBy = Guid.NewGuid();
            await db.SaveChangesAsync(ct);

            categoryAId = categoryA.Id;
            categoryBId = categoryB.Id;
            productAId = productA1.Id;
            reviewToSoftDeleteId = reviewA2.Id;
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenantA.Id, username: usernameA, role: Domain.Enums.UserRole.User);

        var statsResponse = await _client.GetAsync($"/api/v1/categories/{categoryAId}/stats", ct);
        statsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await statsResponse.Content.ReadFromJsonAsync<ProductCategoryStatsResponse>(TestJsonOptions.CaseInsensitive, ct);
        payload.ShouldNotBeNull();
        payload!.CategoryId.ShouldBe(categoryAId);
        payload.ProductCount.ShouldBe(1);
        payload.AveragePrice.ShouldBe(100m);
        payload.TotalReviews.ShouldBe(1);

        // Tenant A token must not access stats of tenant B category.
        var forbiddenByIsolation = await _client.GetAsync($"/api/v1/categories/{categoryBId}/stats", ct);
        forbiddenByIsolation.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Ensure soft-deleted review is hidden from tenant-scoped query path.
        var reviewById = await _client.GetAsync($"/api/v1/productreviews/{reviewToSoftDeleteId}", ct);
        reviewById.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var reviewsByProduct = await _client.GetAsync($"/api/v1/productreviews/by-product/{productAId}", ct);
        reviewsByProduct.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reviews = await reviewsByProduct.Content.ReadFromJsonAsync<ProductReviewResponse[]>(TestJsonOptions.CaseInsensitive, ct);
        reviews.ShouldNotBeNull();
        reviews!.Length.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteProduct_WithExistingReviews_SoftDeletesReviews_Cascade()
    {
        var ct = TestContext.Current.CancellationToken;
        var username = $"cascade-{Guid.NewGuid():N}";
        var (tenant, seededUser) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "pass-cascade",
            ct: ct);

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
            await db.SaveChangesAsync(ct);
            categoryId = category.Id;
        }

        IntegrationAuthHelper.Authenticate(_client, userId: seededUser.Id, tenantId: tenant.Id, username: username, role: Domain.Enums.UserRole.User);

        var createProductResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                Name = $"Product-Cascade-{Guid.NewGuid():N}",
                Price = 88m,
                CategoryId = categoryId
            },
            ct);
        createProductResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdProduct = await createProductResponse.Content.ReadFromJsonAsync<ProductResponse>(TestJsonOptions.CaseInsensitive, ct);
        createdProduct.ShouldNotBeNull();
        productId = createdProduct!.Id;

        var createReview1 = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = productId, Rating = 5 },
            ct);
        createReview1.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdReview1 = await createReview1.Content.ReadFromJsonAsync<ProductReviewResponse>(TestJsonOptions.CaseInsensitive, ct);
        createdReview1.ShouldNotBeNull();
        review1Id = createdReview1!.Id;

        var createReview2 = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = productId, Rating = 4 },
            ct);
        createReview2.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdReview2 = await createReview2.Content.ReadFromJsonAsync<ProductReviewResponse>(TestJsonOptions.CaseInsensitive, ct);
        createdReview2.ShouldNotBeNull();
        review2Id = createdReview2!.Id;

        var deleteResponse = await _client.DeleteAsync($"/api/v1/products/{productId}", ct);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reviewsResponse = await _client.GetAsync($"/api/v1/productreviews/by-product/{productId}", ct);
        reviewsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var visibleReviews = await reviewsResponse.Content.ReadFromJsonAsync<ProductReviewResponse[]>(TestJsonOptions.CaseInsensitive, ct);
        visibleReviews.ShouldNotBeNull();
        visibleReviews.ShouldBeEmpty();

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var allReviews = await verifyDb.ProductReviews
            .IgnoreQueryFilters()
            .Where(r => r.Id == review1Id || r.Id == review2Id)
            .ToListAsync(ct);

        allReviews.Count.ShouldBe(2);
        allReviews.All(r => r.IsDeleted).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteProduct_SoftDeletePipeline_SetsAuditFieldsAndKeepsDependentsPhysicallyPresent()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Code = $"tenant-soft-{Guid.NewGuid():N}", Name = "Tenant Soft Delete" };
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = $"user-soft-{Guid.NewGuid():N}",
            Email = $"soft-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash"
        };
        var category = new Category { Id = Guid.NewGuid(), TenantId = tenantId, Name = $"Category-Soft-{Guid.NewGuid():N}" };
        var product = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Name = $"Product-Soft-{Guid.NewGuid():N}", Price = 50m, CategoryId = category.Id };
        var review1 = new ProductReview { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id, UserId = user.Id, Rating = 5 };
        var review2 = new ProductReview { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id, UserId = user.Id, Rating = 3 };

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Users.Add(user);
            seedContext.Categories.Add(category);
            seedContext.Products.Add(product);
            seedContext.ProductReviews.AddRange(review1, review2);
            await seedContext.SaveChangesAsync(ct);
        }

        await using (var deleteContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var repository = new ProductRepository(deleteContext);
            var unitOfWork = CreateUnitOfWork(deleteContext);

            await repository.DeleteAsync(product.Id, ct);
            await unitOfWork.CommitAsync(ct);
        }

        await using var tenantContext = await CreateDbContextAsync(true, tenantId, actorId, ct);
        await using var unrestrictedContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);

        var visibleReviews = await tenantContext.ProductReviews.Where(r => r.ProductId == product.Id).ToListAsync(ct);
        visibleReviews.ShouldBeEmpty();

        var deletedProduct = await unrestrictedContext.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == product.Id, ct);
        var deletedReviews = await unrestrictedContext.ProductReviews
            .IgnoreQueryFilters()
            .Where(r => r.ProductId == product.Id)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        deletedProduct.IsDeleted.ShouldBeTrue();
        deletedProduct.DeletedAtUtc.ShouldNotBeNull();
        deletedProduct.DeletedBy.ShouldBe(actorId);
        deletedReviews.Count.ShouldBe(2);
        deletedReviews.All(r => r.IsDeleted).ShouldBeTrue();
        deletedReviews.All(r => r.DeletedAtUtc.HasValue).ShouldBeTrue();
        deletedReviews.All(r => r.DeletedBy == actorId).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteProduct_SoftDeletesProductDataLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Code = $"tenant-links-{Guid.NewGuid():N}", Name = "Tenant Links" };
        var category = new Category { Id = Guid.NewGuid(), TenantId = tenantId, Name = $"Category-Links-{Guid.NewGuid():N}" };
        var product = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Name = $"Product-Links-{Guid.NewGuid():N}", Price = 50m, CategoryId = category.Id };
        var productDataId = Guid.NewGuid();

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Categories.Add(category);
            seedContext.Products.Add(product);
            seedContext.ProductDataLinks.Add(new ProductDataLink
            {
                ProductId = product.Id,
                ProductDataId = productDataId,
                TenantId = tenantId
            });
            await seedContext.SaveChangesAsync(ct);
        }

        await using (var deleteContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var handler = new ProductRequestHandlers(
                new ProductRepository(deleteContext),
                Mock.Of<ICategoryRepository>(),
                Mock.Of<IProductDataRepository>(),
                new ProductDataLinkRepository(deleteContext, new TestTenantProvider(tenantId, true)),
                CreateUnitOfWork(deleteContext),
                Mock.Of<IPublisher>());

            await handler.Handle(new DeleteProductCommand(product.Id), ct);
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        var remainingLinks = await verifyContext.ProductDataLinks
            .IgnoreQueryFilters()
            .Where(link => link.ProductId == product.Id)
            .ToListAsync(ct);

        remainingLinks.Count.ShouldBe(1);
        remainingLinks[0].IsDeleted.ShouldBeTrue();
        remainingLinks[0].DeletedAtUtc.ShouldNotBeNull();
        remainingLinks[0].DeletedBy.ShouldBe(actorId);
    }

    [Fact]
    public async Task ProductReviewCreate_WhenRepositoryThrowsAfterTracking_RollsBackTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Code = $"tenant-tx-{Guid.NewGuid():N}", Name = "Tenant Transaction" };
        var category = new Category { Id = Guid.NewGuid(), TenantId = tenantId, Name = $"Category-Tx-{Guid.NewGuid():N}" };
        var product = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Name = $"Product-Tx-{Guid.NewGuid():N}", Price = 99m, CategoryId = category.Id };

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Categories.Add(category);
            seedContext.Products.Add(product);
            await seedContext.SaveChangesAsync(ct);
        }

        var expectedMessage = $"forced-after-add-{Guid.NewGuid():N}";

        await using (var transactionContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var productRepository = new ProductRepository(transactionContext);
            var failingReviewRepository = new Mock<IProductReviewRepository>();
            failingReviewRepository
                .Setup(repository => repository.AddAsync(It.IsAny<ProductReview>(), It.IsAny<CancellationToken>()))
                .Returns((ProductReview entity, CancellationToken _) =>
                {
                    transactionContext.ProductReviews.Add(entity);
                    throw new InvalidOperationException(expectedMessage);
                });
            var unitOfWork = CreateUnitOfWork(transactionContext);
            var handler = new ProductReviewRequestHandlers(
                failingReviewRepository.Object,
                productRepository,
                unitOfWork,
                new TestActorProvider(actorId),
                Mock.Of<IPublisher>());

            var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
                handler.Handle(new CreateProductReviewCommand(new CreateProductReviewRequest(product.Id, "rollback", 4)), ct));

            ex.Message.ShouldBe(expectedMessage);
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        var storedReviews = await verifyContext.ProductReviews
            .IgnoreQueryFilters()
            .Where(r => r.ProductId == product.Id)
            .ToListAsync(ct);

        storedReviews.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnitOfWork_WhenNestedTransactionFailsAndIsCaught_RollsBackInnerWorkToSavepoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Code = $"tenant-savepoint-{Guid.NewGuid():N}", Name = "Tenant Savepoint" };

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        Guid outerCategoryAId = Guid.NewGuid();
        Guid outerCategoryBId = Guid.NewGuid();
        Guid innerCategoryId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                dbContext.Categories.Add(new Category
                {
                    Id = outerCategoryAId,
                    TenantId = tenantId,
                    Name = $"Outer-A-{Guid.NewGuid():N}"
                });

                try
                {
                    await unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        dbContext.Categories.Add(new Category
                        {
                            Id = innerCategoryId,
                            TenantId = tenantId,
                            Name = $"Inner-{Guid.NewGuid():N}"
                        });

                        await Task.CompletedTask;
                        throw new InvalidOperationException("force nested rollback");
                    }, ct);
                }
                catch (InvalidOperationException ex)
                {
                    ex.Message.ShouldBe("force nested rollback");
                }

                dbContext.Categories.Add(new Category
                {
                    Id = outerCategoryBId,
                    TenantId = tenantId,
                    Name = $"Outer-B-{Guid.NewGuid():N}"
                });
            }, ct);
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        var storedCategoryIds = await verifyContext.Categories
            .IgnoreQueryFilters()
            .Where(c => c.Id == outerCategoryAId || c.Id == outerCategoryBId || c.Id == innerCategoryId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        storedCategoryIds.ShouldContain(outerCategoryAId);
        storedCategoryIds.ShouldContain(outerCategoryBId);
        storedCategoryIds.ShouldNotContain(innerCategoryId);
    }

    [Fact]
    public async Task UnitOfWork_WhenTransactionalWriteThrows_RollsBackAllStagedEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Code = $"tenant-full-rollback-{Guid.NewGuid():N}", Name = "Tenant Full Rollback" };

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        Guid categoryId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            var act = () => unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                dbContext.Categories.Add(new Category
                {
                    Id = categoryId,
                    TenantId = tenantId,
                    Name = $"Rollback-Category-{Guid.NewGuid():N}"
                });

                dbContext.Products.Add(new Product
                {
                    Id = productId,
                    TenantId = tenantId,
                    Name = $"Rollback-Product-{Guid.NewGuid():N}",
                    Price = 10m,
                    CategoryId = categoryId
                });

                await Task.CompletedTask;
                throw new InvalidOperationException("force outer rollback");
            }, ct);

            await Should.ThrowAsync<InvalidOperationException>(act);
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        (await verifyContext.Categories.IgnoreQueryFilters().CountAsync(c => c.Id == categoryId, ct)).ShouldBe(0);
        (await verifyContext.Products.IgnoreQueryFilters().CountAsync(p => p.Id == productId, ct)).ShouldBe(0);
    }

    [Fact]
    public async Task UnitOfWork_WithPerCallTransactionOptions_CommitsSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Code = $"tenant-options-{Guid.NewGuid():N}", Name = "Tenant Options" };
        var categoryId = Guid.NewGuid();

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                dbContext.Categories.Add(new Category
                {
                    Id = categoryId,
                    TenantId = tenantId,
                    Name = $"Options-Category-{Guid.NewGuid():N}"
                });

                await Task.CompletedTask;
            },
            ct,
            new TransactionOptions
            {
                IsolationLevel = System.Data.IsolationLevel.Serializable,
                TimeoutSeconds = 15,
                RetryEnabled = false
            });
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        (await verifyContext.Categories.IgnoreQueryFilters().CountAsync(c => c.Id == categoryId, ct)).ShouldBe(1);
    }

    [Fact]
    public async Task UnitOfWork_WhenCommitIsCalledInsideOuterTransaction_ThrowsAndRollsBack()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Code = $"tenant-commit-outer-{Guid.NewGuid():N}", Name = "Tenant Commit Outer" };

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        var categoryId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    dbContext.Categories.Add(new Category
                    {
                        Id = categoryId,
                        TenantId = tenantId,
                        Name = $"Commit-Outer-{Guid.NewGuid():N}"
                    });

                    await unitOfWork.CommitAsync(ct);
                }, ct));

            ex.Message.ShouldContain("CommitAsync cannot be called inside ExecuteInTransactionAsync");
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        (await verifyContext.Categories.IgnoreQueryFilters().CountAsync(c => c.Id == categoryId, ct)).ShouldBe(0);
    }

    [Fact]
    public async Task UnitOfWork_WhenCommitIsCalledInsideNestedTransaction_ThrowsAndRollsBackOuterTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Code = $"tenant-commit-inner-{Guid.NewGuid():N}", Name = "Tenant Commit Inner" };

        await using (var seedContext = await CreateDbContextAsync(hasTenant: false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            await seedContext.SaveChangesAsync(ct);
        }

        var outerCategoryId = Guid.NewGuid();
        var innerCategoryId = Guid.NewGuid();

        await using (var dbContext = await CreateDbContextAsync(true, tenantId, actorId, ct))
        {
            var unitOfWork = CreateUnitOfWork(dbContext);

            var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    dbContext.Categories.Add(new Category
                    {
                        Id = outerCategoryId,
                        TenantId = tenantId,
                        Name = $"Commit-Outer-{Guid.NewGuid():N}"
                    });

                    await unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        dbContext.Categories.Add(new Category
                        {
                            Id = innerCategoryId,
                            TenantId = tenantId,
                            Name = $"Commit-Inner-{Guid.NewGuid():N}"
                        });

                        await unitOfWork.CommitAsync(ct);
                    }, ct);
                }, ct));

            ex.Message.ShouldContain("CommitAsync cannot be called inside ExecuteInTransactionAsync");
        }

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        (await verifyContext.Categories.IgnoreQueryFilters().CountAsync(c => c.Id == outerCategoryId, ct)).ShouldBe(0);
        (await verifyContext.Categories.IgnoreQueryFilters().CountAsync(c => c.Id == innerCategoryId, ct)).ShouldBe(0);
    }

    [Fact]
    public async Task CategoryStats_FunctionCallable_ReturnsZeroValuesForEmptyCategory()
    {
        var ct = TestContext.Current.CancellationToken;
        var username = $"stats-smoke-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "pass-smoke",
            ct: ct);

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
            await db.SaveChangesAsync(ct);
            categoryId = category.Id;
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenant.Id, username: username, role: Domain.Enums.UserRole.User);

        var response = await _client.GetAsync($"/api/v1/categories/{categoryId}/stats", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductCategoryStatsResponse>(TestJsonOptions.CaseInsensitive, ct);
        payload.ShouldNotBeNull();
        payload!.CategoryId.ShouldBe(categoryId);
        payload.ProductCount.ShouldBe(0);
        payload.AveragePrice.ShouldBe(0m);
        payload.TotalReviews.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteProductData_SoftDeletesLinksAndMongoDocument()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var productDataId = Guid.NewGuid();
        var mongoRepositoryMock = _factory.Services.GetRequiredService<Mock<IProductDataRepository>>();
        mongoRepositoryMock.Reset();
        mongoRepositoryMock
            .Setup(r => r.GetByIdAsync(productDataId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageProductData { Id = productDataId, TenantId = tenantId, Title = "Image" });

        var tenant = new Tenant { Id = tenantId, Code = $"tenant-pd-{Guid.NewGuid():N}", Name = "Tenant ProductData" };
        var category = new Category { Id = Guid.NewGuid(), TenantId = tenantId, Name = $"Category-PD-{Guid.NewGuid():N}" };
        var product = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Name = $"Product-PD-{Guid.NewGuid():N}", Price = 50m, CategoryId = category.Id };

        await using (var seedContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct))
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Categories.Add(category);
            seedContext.Products.Add(product);
            seedContext.ProductDataLinks.Add(new ProductDataLink
            {
                ProductId = product.Id,
                ProductDataId = productDataId,
                TenantId = tenantId
            });
            await seedContext.SaveChangesAsync(ct);
        }

        IntegrationAuthHelper.Authenticate(_client, tenantId: tenantId);
        var response = await _client.DeleteAsync($"/api/v1/product-data/{productDataId}", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var verifyContext = await CreateDbContextAsync(false, Guid.Empty, actorId, ct);
        var link = await verifyContext.ProductDataLinks
            .IgnoreQueryFilters()
            .SingleAsync(existing => existing.ProductId == product.Id && existing.ProductDataId == productDataId, ct);

        link.IsDeleted.ShouldBeTrue();
        mongoRepositoryMock.Verify(
            r => r.SoftDeleteAsync(productDataId, It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private async Task<AppDbContext> CreateDbContextAsync(bool hasTenant, Guid tenantId, Guid actorId, CancellationToken ct)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()
            ?? throw new InvalidOperationException("Postgres connection string was not available.");
        var transactionDefaults = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TransactionDefaultsOptions>>().Value;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceServiceCollectionExtensions.ConfigurePostgresDbContext(optionsBuilder, connectionString, transactionDefaults);
        var options = optionsBuilder.Options;

        var stateManager = new AuditableEntityStateManager();
        var context = new AppDbContext(
            options,
            new TestTenantProvider(tenantId, hasTenant),
            new TestActorProvider(actorId),
            TimeProvider.System,
            [new ProductSoftDeleteCascadeRule()],
            new AppUserEntityNormalizationService(),
            stateManager,
            new SoftDeleteProcessor(stateManager));

        await context.Database.OpenConnectionAsync(ct);
        return context;
    }

    private static UnitOfWork CreateUnitOfWork(AppDbContext dbContext)
        => new(
            dbContext,
            Options.Create(new TransactionDefaultsOptions()),
            NullLogger<UnitOfWork>.Instance,
            new EfCoreTransactionProvider(dbContext));

    private sealed class TestTenantProvider(Guid tenantId, bool hasTenant) : ITenantProvider
    {
        public Guid TenantId => tenantId;
        public bool HasTenant => hasTenant;
    }

    private sealed class TestActorProvider(Guid actorId) : IActorProvider
    {
        public Guid ActorId => actorId;
    }
}
