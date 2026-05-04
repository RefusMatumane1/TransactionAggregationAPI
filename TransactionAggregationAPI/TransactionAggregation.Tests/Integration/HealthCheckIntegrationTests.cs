using FluentAssertions;
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

        [Fact]
        public async Task Health_Endpoint_Returns200()
        {
            var response = await _client.GetAsync("/health");

            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        }

        [Fact]
        public async Task Alive_Endpoint_Returns200()
        {
            var response = await _client.GetAsync("/alive");

            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        }
    }
}
