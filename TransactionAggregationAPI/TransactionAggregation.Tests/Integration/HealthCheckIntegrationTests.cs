using FluentAssertions;
using System.Net;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class HealthCheckIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private readonly HttpClient _client;

        public HealthCheckIntegrationTests(IntegrationTestWebAppFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Theory]
        [InlineData("/liveness")]
        [InlineData("/readiness")]
        [InlineData("/health")]
        public async Task HealthEndpoints_AreServed_WithoutAuthentication(string path)
        {
            var response = await _client.GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}