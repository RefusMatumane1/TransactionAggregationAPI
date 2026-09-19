using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using BuildingBlocks.Messaging.Persistence;
using Modules.WebhookSources.Persistence;
using TransactionAggregation.Persistence;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// Spins up a real, throwaway PostgreSQL container and applies the actual EF Core
    /// migrations against it once per test class — closing the gap flagged repeatedly
    /// in docs/failure-scenarios.md and docs/production-readiness-checklist.md: the
    /// Inbox/Outbox claim SQL (FOR UPDATE SKIP LOCKED) and the unique-constraint
    /// idempotency guarantee are both Postgres-specific and were previously verified
    /// only manually (docker run + docker exec psql), not by any automated test.
    ///
    /// Three DbContexts now migrate against this one database, each owning its own
    /// schema/migration history — see docs/adr/0009-schema-per-module-database-strategy.md.
    /// </summary>
    public sealed class PostgresContainerFixture : IAsyncLifetime
    {
        private PostgreSqlContainer _container = null!;

        public string ConnectionString { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("transactiondb")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();

            using var messaging = CreateMessagingContext();
            await messaging.Database.MigrateAsync();

            using var webhookSources = CreateWebhookSourcesContext();
            await webhookSources.Database.MigrateAsync();

            using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
                await _container.DisposeAsync();
        }

        /// <summary>
        /// ApplicationDbContext.SaveChangesAsync flushes the exact MessagingDbContext
        /// instance it was constructed with (see the Outbox-atomicity comment on that
        /// class) — a caller that separately creates its own MessagingDbContext to add
        /// an OutboxMessage MUST pass that same instance here, or the write silently
        /// never reaches the database (a different, untracked instance gets flushed
        /// instead). Omit messagingDbContext only when the test never touches Outbox.
        /// </summary>
        public ApplicationDbContext CreateContext(MessagingDbContext? messagingDbContext = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            var mediator = Substitute.For<IMediator>();
            mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new ApplicationDbContext(options, mediator, messagingDbContext ?? CreateMessagingContext());
        }

        public MessagingDbContext CreateMessagingContext()
        {
            var options = new DbContextOptionsBuilder<MessagingDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            var mediator = Substitute.For<IMediator>();
            mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new MessagingDbContext(options, mediator);
        }

        public WebhookSourcesDbContext CreateWebhookSourcesContext()
        {
            var options = new DbContextOptionsBuilder<WebhookSourcesDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            var mediator = Substitute.For<IMediator>();
            mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new WebhookSourcesDbContext(options, mediator);
        }
    }

    [CollectionDefinition(Name)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
    {
        public const string Name = "Postgres";
    }
}
