using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Modules.Audit.Application.DTOs;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Contracts;
using Modules.Audit.Domain;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    /// <summary>
    /// End to end through the real HTTP pipeline: what the webhook endpoint writes to the
    /// audit trail for each outcome, and that the admin audit API is admin-only and serves it.
    /// Background dispatchers don't run in this host, so accepted deliveries are asserted as
    /// queued audit outbox messages; refused ones are written directly and asserted as rows.
    /// </summary>
    public class AuditApiIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private const string WebhookPath = "/api/v1/webhooks/bank-aggregator/transactions";
        private const string AuditEventsPath = "/api/v1/admin/audit/events";

        private readonly IntegrationTestWebAppFactory _factory;

        public AuditApiIntegrationTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
        }

        private async Task<string> SeedActiveWebhookSourceAsync(string name)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IWebhookSourcesDbContext>();
            var (source, apiKey) = WebhookSource.Create(name, TestInstitutions.All);
            context.WebhookSources.Add(source);
            await context.SaveChangesAsync();
            return apiKey;
        }

        private static HttpRequestMessage Webhook(object payload, string? apiKey, string userAgent)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath) { Content = JsonContent.Create(payload) };
            if (apiKey is not null)
                request.Headers.Add("X-Api-Key", apiKey);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            return request;
        }

        private static object Payload(string externalAccountId, int count = 1) => new
        {
            ExternalAccountId = externalAccountId,
            Transactions = Enumerable.Range(1, count).Select(i => new
            {
                Id = $"txn-{i}",
                Amount = -150.00m,
                Currency = "ZAR",
                Description = "Woolworths",
                Category = (string?)null,
                Date = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
            }).ToArray()
        };

        private List<AuditEvent> StoredAuditEvents()
        {
            using var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IAuditDbContext>().AuditEvents.ToList();
        }

        private List<AuditEventRecord> QueuedAuditEvents()
        {
            using var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IMessagingDbContext>().OutboxMessages
                .Where(m => m.Type == AuditOutbox.MessageType)
                .AsEnumerable()
                .SelectMany(m => JsonSerializer.Deserialize<AuditOutboxPayload>(m.Payload)!.Events)
                .ToList();
        }

        private HttpClient Client(string? roles = null)
        {
            var client = _factory.CreateClient();
            if (roles is not null)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, Guid.NewGuid().ToString());
                client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, roles);
            }
            return client;
        }

        [Fact]
        public async Task Webhook_UnknownApiKey_IsAuditedAsUnauthorizedWithoutTheKey()
        {
            var marker = $"probe/{Guid.NewGuid():N}";
            using var client = Client();

            var response = await client.SendAsync(Webhook(Payload("ext-a"), "guessed-key-123", marker));

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var audit = StoredAuditEvents().Should()
                .ContainSingle(e => e.Metadata.GetValueOrDefault("userAgent") == marker).Subject;
            audit.EventType.Should().Be(AuditEventTypes.InboundUnauthorized);
            audit.Channel.Should().Be(AuditChannels.Webhook);
            audit.Detail.Should().Be("Unknown or inactive API key");
            audit.Metadata.Values.Should().NotContain(v => v.Contains("guessed-key-123"), "presented keys must never be written to the audit trail");
        }

        [Fact]
        public async Task Webhook_MissingApiKey_IsAuditedAsUnauthorized()
        {
            var marker = $"probe/{Guid.NewGuid():N}";
            using var client = Client();

            await client.SendAsync(Webhook(Payload("ext-a"), apiKey: null, marker));

            StoredAuditEvents().Should().ContainSingle(e => e.Metadata.GetValueOrDefault("userAgent") == marker)
                .Which.Detail.Should().Be("Missing X-Api-Key header");
        }

        [Fact]
        public async Task Webhook_ValidationFailure_IsAuditedAsRejected()
        {
            var source = $"audit-reject-{Guid.NewGuid():N}";
            var apiKey = await SeedActiveWebhookSourceAsync(source);
            using var client = Client();

            var response = await client.SendAsync(Webhook(Payload("ext-reject", count: 501), apiKey, "aggregator/1.0"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var audit = StoredAuditEvents().Should().ContainSingle(e => e.SourceName == source).Subject;
            audit.EventType.Should().Be(AuditEventTypes.InboundRejected);
            audit.ExternalAccountId.Should().Be("ext-reject");
            audit.Detail.Should().Contain("500");
            audit.Metadata.Should().Contain("transactionCount", "501");
        }

        [Fact]
        public async Task Webhook_Accepted_QueuesAReceivedAuditEventWithWebhookMetadata()
        {
            var source = $"audit-accept-{Guid.NewGuid():N}";
            var apiKey = await SeedActiveWebhookSourceAsync(source);
            using var client = Client();
            var request = Webhook(Payload("ext-accept"), apiKey, "aggregator/2.0");
            request.Headers.Add("Idempotency-Key", "delivery-42");

            var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var audit = QueuedAuditEvents().Should().ContainSingle(e => e.SourceName == source).Subject;
            audit.EventType.Should().Be(AuditEventTypes.InboundReceived);
            audit.Channel.Should().Be(AuditChannels.Webhook);
            audit.IdempotencyKey.Should().Be("delivery-42");
            audit.Metadata.Should().Contain("userAgent", "aggregator/2.0")
                .And.Contain("httpMethod", "POST")
                .And.Contain("idempotencyKeyProvided", "true")
                .And.ContainKey("requestId");
        }

        [Fact]
        public async Task AuditApi_Anonymous_Returns401_NonAdmin_Returns403()
        {
            using var anonymous = Client();
            using var customer = Client(roles: "");

            (await anonymous.GetAsync(AuditEventsPath)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await customer.GetAsync(AuditEventsPath)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task AuditApi_Admin_CanSearchByChannelAndSource()
        {
            var source = $"audit-search-{Guid.NewGuid():N}";
            var apiKey = await SeedActiveWebhookSourceAsync(source);
            using (var client = Client())
                await client.SendAsync(Webhook(Payload("ext-search", count: 501), apiKey, "aggregator/1.0"));

            using var admin = Client(roles: "admin");
            var response = await admin.GetAsync($"{AuditEventsPath}?channel=webhook&sourceName={source}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var page = await response.Content.ReadFromJsonAsync<AuditEventPage>();
            page!.TotalCount.Should().Be(1);
            page.Items.Single().EventType.Should().Be(AuditEventTypes.InboundRejected);
        }

        [Fact]
        public async Task AuditApi_Admin_UnknownTransactionLineage_Returns404()
        {
            using var admin = Client(roles: "admin");

            var response = await admin.GetAsync($"/api/v1/admin/audit/transactions/{Guid.NewGuid()}/lineage");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}