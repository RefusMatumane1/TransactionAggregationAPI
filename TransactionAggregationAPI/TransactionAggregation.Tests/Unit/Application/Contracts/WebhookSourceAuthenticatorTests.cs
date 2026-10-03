using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Modules.WebhookSources.Application.Contracts;
using Modules.WebhookSources.Domain;
using Modules.WebhookSources.Infrastructure.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Contracts
{
    public class WebhookSourceAuthenticatorTests
    {
        private sealed class SaveCounter : SaveChangesInterceptor
        {
            public int Saves { get; private set; }

            public override ValueTask<int> SavedChangesAsync(
                SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
            {
                Saves++;
                return ValueTask.FromResult(result);
            }
        }

        [Fact]
        public async Task BackToBackDeliveries_WriteLastUsedAtOnce_NotOnEveryRequest()
        {
            var counter = new SaveCounter();
            var options = new DbContextOptionsBuilder<WebhookSourcesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(counter)
                .Options;
            await using var context = new WebhookSourcesDbContext(options, new RecordingAuditTrail());
            var (source, apiKey) = WebhookSource.Create("busy-provider", "busy-provider", "#123456");
            context.WebhookSources.Add(source);
            await context.SaveChangesAsync();
            var savesAfterSeeding = counter.Saves;

            var authenticator = new WebhookSourceAuthenticator(context);
            for (var delivery = 0; delivery < 10; delivery++)
                (await authenticator.AuthenticateAsync(apiKey)).Should().Be("busy-provider");

            (counter.Saves - savesAfterSeeding).Should().Be(1,
                "only the first delivery in the interval records usage; the rest authenticate without writing");
            source.LastUsedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task UnknownKey_IsRejected_WithoutWriting()
        {
            var counter = new SaveCounter();
            var options = new DbContextOptionsBuilder<WebhookSourcesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(counter)
                .Options;
            await using var context = new WebhookSourcesDbContext(options, new RecordingAuditTrail());

            (await new WebhookSourceAuthenticator(context).AuthenticateAsync("whsk_not-a-real-key")).Should().BeNull();

            counter.Saves.Should().Be(0);
        }
    }
}