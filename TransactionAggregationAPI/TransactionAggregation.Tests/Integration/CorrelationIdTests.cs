using FluentAssertions;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class CorrelationIdTests(IntegrationTestWebAppFactory factory) : IClassFixture<IntegrationTestWebAppFactory>
    {
        private const string Header = "X-Correlation-Id";

        private async Task<string?> RoundTripAsync(string? supplied)
        {
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/transactions/" + Guid.NewGuid());
            if (supplied is not null)
                request.Headers.TryAddWithoutValidation(Header, supplied);

            var response = await client.SendAsync(request);
            return response.Headers.TryGetValues(Header, out var values) ? values.Single() : null;
        }

        [Fact]
        public async Task SafeCallerId_IsEchoedBack() =>
            (await RoundTripAsync("order-123_abc.4")).Should().Be("order-123_abc.4");

        [Fact]
        public async Task NoCallerId_ResponseStillCarriesOne() =>
            (await RoundTripAsync(null)).Should().NotBeNullOrWhiteSpace();

        [Theory]
        [InlineData("has spaces and {braces}")]
        [InlineData("tooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooong")]
        public async Task UnsafeCallerId_IsReplaced_NotLogged(string supplied)
        {
            var echoed = await RoundTripAsync(supplied);

            echoed.Should().NotBeNullOrWhiteSpace().And.NotBe(supplied,
                "client-supplied ids land in every log line, so only short plain tokens are trusted");
        }
    }
}