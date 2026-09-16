using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TransactionAggregation.Application.Common.Inbox;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Persistence;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class WebhookApiIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private const string WebhookPath = "/api/v1/webhooks/bank-aggregator/transactions";

        private readonly IntegrationTestWebAppFactory _factory;
        private readonly HttpClient _client;

        public WebhookApiIntegrationTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<BankLink> SeedActiveBankLinkAsync(string externalAccountId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Webhook Test User");
            context.Customers.Add(customer);

            var link = BankLink.Create(customer.Id, Institution.FNB);
            link.Activate(AccountId.Create(), externalAccountId, "enc-access", "enc-refresh", DateTime.UtcNow.AddHours(1));
            context.BankLinks.Add(link);

            await context.SaveChangesAsync();
            return link;
        }

        /// <summary>Seeds a real, active WebhookSource row (the DB-backed replacement for the
        /// old config-based key) and returns the one-time plaintext key it authenticates with.</summary>
        private async Task<string> SeedActiveWebhookSourceAsync(string name)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var (source, apiKey) = WebhookSource.Create(name);
            context.WebhookSources.Add(source);
            await context.SaveChangesAsync();

            return apiKey;
        }

        private static object SampleTransaction(string id = "txn-1") => new
        {
            Id = id,
            Amount = -150.00m,
            Currency = "ZAR",
            Description = "Woolworths",
            Category = (string?)null,
            Date = DateTime.UtcNow
        };

        private static HttpRequestMessage BuildRequest(object payload, string? apiKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
            {
                Content = JsonContent.Create(payload)
            };
            if (apiKey is not null)
                request.Headers.Add("X-Api-Key", apiKey);
            return request;
        }

        // ── Auth ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task ReceiveTransactions_MissingApiKey_Returns401()
        {
            using var request = BuildRequest(
                new { ExternalAccountId = "ext-1", Transactions = new[] { SampleTransaction() } }, apiKey: null);

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ReceiveTransactions_WrongApiKey_Returns401()
        {
            // A real, active source exists — proves this 401 is genuinely "key doesn't match
            // any source", not just "no sources configured yet".
            await SeedActiveWebhookSourceAsync("some-other-source");

            using var request = BuildRequest(
                new { ExternalAccountId = "ext-1", Transactions = new[] { SampleTransaction() } }, apiKey: "wrong-key");

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ReceiveTransactions_DeactivatedSourcesKey_Returns401()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var (source, apiKey) = WebhookSource.Create("soon-deactivated");
            source.Deactivate();
            context.WebhookSources.Add(source);
            await context.SaveChangesAsync();

            using var request = BuildRequest(
                new { ExternalAccountId = "ext-1", Transactions = new[] { SampleTransaction() } }, apiKey: apiKey);

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // ── Receipt (business validation is deferred to the worker — see
        // ProcessInboundTransactionsCommandHandlerTests for BankLink-resolution/dedup coverage) ──

        [Fact]
        public async Task ReceiveTransactions_ValidKeyAndActiveLink_Returns202AndQueuesInboxMessage()
        {
            var link = await SeedActiveBankLinkAsync("ext-webhook-1");
            var apiKey = await SeedActiveWebhookSourceAsync("source-1");

            using var request = BuildRequest(
                new { ExternalAccountId = link.ExternalAccountId, Transactions = new[] { SampleTransaction("txn-webhook-1") } },
                apiKey);

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var message = context.InboxMessages.Should().ContainSingle(m => m.SourceName == "source-1").Subject;

            var payload = JsonSerializer.Deserialize<InboundTransactionsPayload>(message.Payload)!;
            payload.ExternalAccountId.Should().Be(link.ExternalAccountId);
            payload.Transactions.Should().ContainSingle(t => t.Id == "txn-webhook-1");
        }

        [Fact]
        public async Task ReceiveTransactions_RedeliveredPayload_QueuesASeparateInboxMessageEachTime()
        {
            // The webhook layer no longer dedupes — that guarantee now lives in
            // ProcessInboundTransactionsCommandHandler, which runs later against both queued
            // rows. See ProcessInboundTransactionsCommandHandlerTests.Handle_RedeliveredTransaction_IsSkippedNotDuplicated.
            var link = await SeedActiveBankLinkAsync("ext-webhook-2");
            var apiKey = await SeedActiveWebhookSourceAsync("source-2");
            var payload = new { ExternalAccountId = link.ExternalAccountId, Transactions = new[] { SampleTransaction("txn-webhook-2") } };

            using (var first = BuildRequest(payload, apiKey))
                (await _client.SendAsync(first)).StatusCode.Should().Be(HttpStatusCode.Accepted);

            using (var redelivered = BuildRequest(payload, apiKey))
                (await _client.SendAsync(redelivered)).StatusCode.Should().Be(HttpStatusCode.Accepted);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.InboxMessages.Count(m => m.SourceName == "source-2").Should().Be(2);
        }

        // ── Unknown link ──────────────────────────────────────────────────────

        [Fact]
        public async Task ReceiveTransactions_UnknownExternalAccountId_StillReturns202AndQueuesForProcessing()
        {
            // BankLink existence is business state, not payload shape — checking it moved out of
            // the synchronous webhook path entirely, into ProcessInboundTransactionsCommand. See
            // ProcessInboundTransactionsCommandHandlerTests.Handle_UnknownExternalAccountId_ReturnsNotFound
            // for where an unresolvable account id is actually surfaced (to the dispatcher, as a
            // retry/dead-letter, not synchronously to the caller).
            var apiKey = await SeedActiveWebhookSourceAsync("source-3");

            using var request = BuildRequest(
                new { ExternalAccountId = "never-linked", Transactions = new[] { SampleTransaction() } },
                apiKey);

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.InboxMessages.Should().ContainSingle(m => m.SourceName == "source-3");
        }

        // ── Batch-size cap ───────────────────────────────────────────────────

        [Fact]
        public async Task ReceiveTransactions_MoreThanFiveHundredTransactions_Returns400()
        {
            var apiKey = await SeedActiveWebhookSourceAsync("source-4");
            var transactions = Enumerable.Range(1, 501).Select(i => SampleTransaction($"txn-{i}")).ToArray();

            using var request = BuildRequest(
                new { ExternalAccountId = "ext-1", Transactions = transactions }, apiKey);

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
