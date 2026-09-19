using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    /// <summary>
    /// Instructions.md section 38 requires the API to be "understandable without
    /// reading the source code" — including how to authenticate. These tests pin
    /// the behavior of BearerSecuritySchemeTransformer against the actual generated
    /// document rather than the handler wiring in isolation, since the thing that
    /// can silently break is the RelativePath-to-document.Paths lookup itself
    /// (mismatched leading slash or a stray query string suffix), not the security
    /// scheme construction. Program.cs registers the real JWT "Bearer" scheme
    /// unconditionally (IntegrationTestWebAppFactory only overrides which scheme is
    /// the *default*, so requests authenticate via the "Test" scheme instead), so
    /// the transformer sees the same "Bearer"-named scheme here as in production.
    /// </summary>
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
                .GetProperty("/api/v1/customers/{customerId}")
                .GetProperty("get");

            var security = operation.GetProperty("security");
            security.GetArrayLength().Should().BeGreaterThan(0);
            security[0].EnumerateObject().Select(p => p.Name).Should().Contain("Bearer");
        }

        [Fact]
        public async Task AuthorizedEndpoint_WithGuidRouteConstraint_RequiresBearerScheme()
        {
            // Regression pin: a route template like "{customerId:guid}" must not
            // survive into the lookup — the OpenAPI document's own path keys are
            // unconstrained ("{customerId}"), and a mismatch here means the
            // security requirement silently never gets attached.
            using var document = await GetOpenApiDocumentAsync();

            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty("/api/v1/customers/{customerId}/accounts")
                .GetProperty("post");

            var security = operation.GetProperty("security");
            security.GetArrayLength().Should().BeGreaterThan(0);
            security[0].EnumerateObject().Select(p => p.Name).Should().Contain("Bearer");
        }

        [Fact]
        public async Task AuthorizedEndpoint_MappedAtGroupRoot_RequiresBearerScheme()
        {
            // Regression pin: MapGet("/", ...) under a route group renders with a
            // trailing slash in ApiDescription.RelativePath (".../accounts/") that
            // the OpenAPI document's own path key (".../accounts") doesn't carry.
            using var document = await GetOpenApiDocumentAsync();

            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty("/api/v1/customers/{customerId}/accounts")
                .GetProperty("get");

            var security = operation.GetProperty("security");
            security.GetArrayLength().Should().BeGreaterThan(0);
            security[0].EnumerateObject().Select(p => p.Name).Should().Contain("Bearer");
        }

        [Fact]
        public async Task AnonymousCustomerRegistration_HasNoSecurityRequirement()
        {
            using var document = await GetOpenApiDocumentAsync();

            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty("/api/v1/customers")
                .GetProperty("post");

            operation.TryGetProperty("security", out var security).Should().BeFalse(
                "the registration endpoint is [AllowAnonymous] and must not claim Bearer auth is required");
            _ = security;
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
