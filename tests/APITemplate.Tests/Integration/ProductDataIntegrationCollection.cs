using Xunit;

namespace APITemplate.Tests.Integration;

[CollectionDefinition("Integration.ProductDataController", DisableParallelization = true)]
public sealed class ProductDataIntegrationCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
