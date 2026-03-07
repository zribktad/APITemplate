using APITemplate.Tests.Integration;
using Xunit;

[assembly: AssemblyFixture(typeof(CustomWebApplicationFactory))]
[assembly: AssemblyFixture(typeof(BffSecurityWebApplicationFactory))]
[assembly: AssemblyFixture(typeof(RateLimitingWebApplicationFactory))]
