using BuildingBlocks.Messaging.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Infrastructure.Persistence;
using Modules.BankLinks.Infrastructure.Persistence;
using Modules.Customers.Infrastructure.Persistence;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources.Infrastructure.Persistence;
using Npgsql;
using NSubstitute;
using Testcontainers.PostgreSql;
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
    /// Each module DbContext migrates against this one database, each owning its own
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

            using var bankLinks = CreateBankLinksContext();
            await bankLinks.Database.MigrateAsync();

            using var customers = CreateCustomersContext();
            await customers.Database.MigrateAsync();

            using var context = CreateContext();
            await context.Database.MigrateAsync();

            using var audit = CreateAuditContext();
            await audit.Database.MigrateAsync();
        }

        /// <summary>
        /// A fresh, fully migrated database in the same container, for tests that move the
        /// schema itself (e.g. migrating down and back up) and so must not share data or
        /// migration state with the rest of the collection.
        /// </summary>
        public async Task<string> CreateIsolatedDatabaseAsync()
        {
            var name = $"isolated_{Guid.NewGuid():N}";
            await using (var admin = new NpgsqlConnection(ConnectionString))
            {
                await admin.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
                await create.ExecuteNonQueryAsync();
            }

            var connectionString = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;

            using var messaging = CreateMessagingContext(connectionString);
            await messaging.Database.MigrateAsync();
            using var customers = CreateCustomersContext(connectionString);
            await customers.Database.MigrateAsync();
            using var context = CreateContext(connectionString: connectionString);
            await context.Database.MigrateAsync();

            return connectionString;
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
                await _container.DisposeAsync();
        }

        /// <summary>
        /// TransactionsDbContext.SaveChangesAsync flushes the exact MessagingDbContext
        /// instance it was constructed with (see the Outbox-atomicity comment on that
        /// class) — a caller that separately creates its own MessagingDbContext to add
        /// an OutboxMessage MUST pass that same instance here, or the write silently
        /// never reaches the database (a different, untracked instance gets flushed
        /// instead). Omit messagingDbContext only when the test never touches Outbox.
        ///
        /// Just as important: the returned context is built against the SAME
        /// NpgsqlConnection as the MessagingDbContext (mirroring Program.cs's shared
        /// scoped NpgsqlConnection), not merely the same connection string — EF Core's
        /// Database.UseTransactionAsync throws "the specified transaction is not
        /// associated with the current connection" if the two contexts hold separate
        /// physical connections, even to the same database.
        /// </summary>
        public TransactionsDbContext CreateContext(MessagingDbContext? messagingDbContext = null, string? connectionString = null)
        {
            var messaging = messagingDbContext ?? CreateMessagingContext(connectionString);

            var options = new DbContextOptionsBuilder<TransactionsDbContext>()
                .UseNpgsql((NpgsqlConnection)messaging.Database.GetDbConnection())
                .Options;

            var mediator = Substitute.For<IMediator>();
            mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new TransactionsDbContext(options, mediator, messaging);
        }

        public MessagingDbContext CreateMessagingContext(string? connectionString = null)
        {
            var options = new DbContextOptionsBuilder<MessagingDbContext>()
                .UseNpgsql(connectionString ?? ConnectionString)
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

        public CustomersDbContext CreateCustomersContext(string? connectionString = null)
        {
            var options = new DbContextOptionsBuilder<CustomersDbContext>()
                .UseNpgsql(connectionString ?? ConnectionString)
                .Options;

            var mediator = Substitute.For<IMediator>();
            mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new CustomersDbContext(options, mediator);
        }

        public BankLinksDbContext CreateBankLinksContext()
        {
            var options = new DbContextOptionsBuilder<BankLinksDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            var mediator = Substitute.For<IMediator>();
            mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new BankLinksDbContext(options, mediator);
        }

        public AuditDbContext CreateAuditContext()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            var mediator = Substitute.For<IMediator>();
            mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new AuditDbContext(options, mediator);
        }
    }

    [CollectionDefinition(Name)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
    {
        public const string Name = "Postgres";
    }
}