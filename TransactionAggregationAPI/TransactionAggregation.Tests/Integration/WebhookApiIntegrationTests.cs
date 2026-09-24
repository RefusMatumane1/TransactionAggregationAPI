using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Domain;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain;
using SharedKernel.Common.ValueObjects;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
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
            var context = scope.ServiceProvider.GetRequiredService<ICustomersDbContext>();
            var bankLinksContext = scope.ServiceProvider.GetRequiredService<IBankLinksDbContext>();

            var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Webhook Test User");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var link = BankLink.Create(customer.Id, Institution.FNB);
            link.Activate(AccountId.Create(), externalAccountId, "enc-access", "enc-refresh", DateTime.UtcNow.AddHours(1));
            bankLinksContext.BankLinks.Add(link);

            await bankLinksContext.SaveChangesAsync();
            return link;
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
            var context = scope.ServiceProvider.GetRequiredService<IWebhookSourcesDbContext>();
            var (source, apiKey) = WebhookSource.Create("soon-deactivated", TestInstitutions.All);
            source.Deactivate();
            context.WebhookSources.Add(source);
            await context.SaveChangesAsync();

            using var request = BuildRequest(
                new { ExternalAccountId = "ext-1", Transactions = new[] { SampleTransaction() } }, apiKey: apiKey);

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

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
            var messaging = scope.ServiceProvider.GetRequiredService<IMessagingDbContext>();
            var message = messaging.InboxMessages.Should().ContainSingle(m => m.SourceName == "source-1").Subject;

            var payload = JsonSerializer.Deserialize<InboundTransactionsPayload>(message.Payload)!;
            payload.ExternalAccountId.Should().Be(link.ExternalAccountId);
            payload.Transactions.Should().ContainSingle(t => t.Id == "txn-webhook-1");
        }

        [Fact]
        public async Task ReceiveTransactions_RedeliveredPayload_Returns202AsDuplicateAndQueuesOnce()
        {

            var link = await SeedActiveBankLinkAsync("ext-webhook-2");
            var apiKey = await SeedActiveWebhookSourceAsync("source-2");
            var payload = new { ExternalAccountId = link.ExternalAccountId, Transactions = new[] { SampleTransaction("txn-webhook-2") } };

            using (var first = BuildRequest(payload, apiKey))
            {
                var response = await _client.SendAsync(first);
                response.StatusCode.Should().Be(HttpStatusCode.Accepted);
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isDuplicate").GetBoolean().Should().BeFalse();
            }

            using (var redelivered = BuildRequest(payload, apiKey))
            {
                var response = await _client.SendAsync(redelivered);
                response.StatusCode.Should().Be(HttpStatusCode.Accepted);
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isDuplicate").GetBoolean().Should().BeTrue();
            }

            using var scope = _factory.Services.CreateScope();
            var messaging = scope.ServiceProvider.GetRequiredService<IMessagingDbContext>();
            messaging.InboxMessages.Count(m => m.SourceName == "source-2").Should().Be(1);
        }

        [Fact]
        public async Task ReceiveTransactions_IdempotencyKeyReusedForDifferentPayload_Returns422AndQueuesNothingNew()
        {
            var link = await SeedActiveBankLinkAsync("ext-webhook-5");
            var apiKey = await SeedActiveWebhookSourceAsync("source-5");

            using (var first = BuildRequest(
                new { ExternalAccountId = link.ExternalAccountId, Transactions = new[] { SampleTransaction("txn-webhook-5a") } }, apiKey))
            {
                first.Headers.Add("Idempotency-Key", "delivery-5");
                (await _client.SendAsync(first)).StatusCode.Should().Be(HttpStatusCode.Accepted);
            }

            using (var retried = BuildRequest(
                new { ExternalAccountId = link.ExternalAccountId, Transactions = new[] { SampleTransaction("txn-webhook-5b") } }, apiKey))
            {
                retried.Headers.Add("Idempotency-Key", "delivery-5");
                var response = await _client.SendAsync(retried);
                response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
                    "a key reused for new content is a sender error, not a redelivery to acknowledge");
            }

            using var scope = _factory.Services.CreateScope();
            var messaging = scope.ServiceProvider.GetRequiredService<IMessagingDbContext>();
            messaging.InboxMessages.Should().ContainSingle(m => m.SourceName == "source-5")
                .Which.IdempotencyKey.Should().Be("delivery-5");
        }

        [Fact]
        public async Task ReceiveTransactions_UnknownExternalAccountId_StillReturns202AndQueuesForProcessing()
        {

            var apiKey = await SeedActiveWebhookSourceAsync("source-3");

            using var request = BuildRequest(
                new { ExternalAccountId = "never-linked", Transactions = new[] { SampleTransaction() } },
                apiKey);

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);

            using var scope = _factory.Services.CreateScope();
            var messaging = scope.ServiceProvider.GetRequiredService<IMessagingDbContext>();
            messaging.InboxMessages.Should().ContainSingle(m => m.SourceName == "source-3");
        }

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