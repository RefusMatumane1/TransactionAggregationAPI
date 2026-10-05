using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class OpenApiSecuritySchemeTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private readonly IntegrationTestWebAppFactory _factory;

        public OpenApiSecuritySchemeTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
        }

        private async Task<JsonDocument> GetOpenApiDocumentAsync()
        {
            using var client = _factory.CreateClient();
            var response = await client.GetAsync("/openapi/v1.json");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json);
        }

        [Fact]
        public async Task Document_DeclaresBearerSecurityScheme()
        {
            using var document = await GetOpenApiDocumentAsync();

            var schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
            var bearer = schemes.GetProperty("Bearer");

            bearer.GetProperty("type").GetString().Should().Be("http");
            bearer.GetProperty("scheme").GetString().Should().Be("bearer");
        }

        [Fact]
        public async Task AuthorizedEndpoint_RequiresBearerScheme()
        {
            using var document = await GetOpenApiDocumentAsync();

            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty("/api/v1/transactions/{id}")
                .GetProperty("get");

            var security = operation.GetProperty("security");
            security.GetArrayLength().Should().BeGreaterThan(0);
            security[0].EnumerateObject().Select(p => p.Name).Should().Contain("Bearer");
        }

        [Fact]
        public async Task AuthorizedEndpoint_WithGuidRouteConstraint_RequiresBearerScheme()
        {
            using var document = await GetOpenApiDocumentAsync();

            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty("/api/v1/transactions/{id}")
                .GetProperty("get");

            var security = operation.GetProperty("security");
            security.GetArrayLength().Should().BeGreaterThan(0);
            security[0].EnumerateObject().Select(p => p.Name).Should().Contain("Bearer");
        }

        [Fact]
        public async Task AuthorizedEndpoint_MappedAtGroupRoot_RequiresBearerScheme()
        {
            using var document = await GetOpenApiDocumentAsync();

            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty("/api/v1/transactions")
                .GetProperty("get");

            var security = operation.GetProperty("security");
            security.GetArrayLength().Should().BeGreaterThan(0);
            security[0].EnumerateObject().Select(p => p.Name).Should().Contain("Bearer");
        }

        [Fact]
        public async Task WebhookIngestionEndpoint_HasNoSecurityRequirement()
        {
            using var document = await GetOpenApiDocumentAsync();

            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty("/api/v1/webhooks/bank-aggregator/transactions")
                .GetProperty("post");

            operation.TryGetProperty("security", out _).Should().BeFalse(
                "the webhook endpoint authenticates via API key, not JWT Bearer, and carries no IAuthorizeData");
        }
    }
}