using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

[CollectionDefinition("Integration.Postgres")]
public sealed class PostgresIntegrationCollection : ICollectionFixture<PostgresWebApplicationFactory>
{
}
