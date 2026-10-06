using BuildingBlocks.Messaging.Persistence;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Audit.Application.Contracts;
using Modules.Audit.Contracts;
using Modules.Audit.Infrastructure.Persistence;
using Modules.Customers.Infrastructure.Persistence;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    public sealed class PostgresContainerFixture : IAsyncLifetime
    {
        private PostgreSqlContainer _container = null!;

        public string ConnectionString { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            _container = new PostgreSqlBuilder("postgres:17.6")
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

            using var audit = CreateAuditContext();
            await audit.Database.MigrateAsync();

            using var customers = CreateCustomersContext();
            await customers.Database.MigrateAsync();
        }

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
            using var context = CreateContext(connectionString: connectionString);
            await context.Database.MigrateAsync();
            using var webhookSources = CreateWebhookSourcesContext(connectionString);
            await webhookSources.Database.MigrateAsync();
            using var audit = CreateAuditContext(connectionString);
            await audit.Database.MigrateAsync();
            using var customers = CreateCustomersContext(connectionString);
            await customers.Database.MigrateAsync();

            return connectionString;
        }

        public async Task<string> ExecInContainerAsync(params string[] command)
        {
            var result = await _container.ExecAsync(command);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"'{string.Join(' ', command)}' exited {result.ExitCode}: {result.Stderr}");
            return result.Stdout;
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
                await _container.DisposeAsync();
        }

        public async Task<DailyTotalsRefresh?> RefreshDailyTotalsAsync(string? connectionString = null, TimeSpan? overlap = null)
        {
            using var context = CreateContext(connectionString: connectionString);
            return await new PostgresDailyTotalsRefresher(context, TimeProvider.System)
                .RefreshAsync(overlap ?? TimeSpan.FromMinutes(10), CancellationToken.None);
        }

        public TransactionsDbContext CreateContext(
            MessagingDbContext? messagingDbContext = null, string? connectionString = null, params IInterceptor[] interceptors)
        {
            var messaging = messagingDbContext ?? CreateMessagingContext(connectionString);

            var options = new DbContextOptionsBuilder<TransactionsDbContext>()
                .UseNpgsql((NpgsqlConnection)messaging.Database.GetDbConnection())
                .AddInterceptors(interceptors)
                .Options;

            return new TransactionsDbContext(options, messaging, CreateAuditTrail(messaging));
        }

        public TransactionsDbContext CreateRetryingContext(MessagingDbContext messaging)
        {
            var options = new DbContextOptionsBuilder<TransactionsDbContext>()
                .UseNpgsql((NpgsqlConnection)messaging.Database.GetDbConnection(),
                    npgsql => npgsql.UseModuleDefaults<TransactionsDbContext>())
                .Options;

            return new TransactionsDbContext(options, messaging, CreateAuditTrail(messaging, retrying: true));
        }

        public IAuditTrail CreateAuditTrail(MessagingDbContext messaging, bool retrying = false)
        {
            var connection = (NpgsqlConnection)messaging.Database.GetDbConnection();
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseNpgsql(connection, npgsql =>
                {
                    if (retrying)
                        npgsql.UseModuleDefaults<AuditDbContext>();
                })
                .Options;

            return new AuditTrail(new AuditDbContext(options), NullLogger<AuditTrail>.Instance);
        }

        public MessagingDbContext CreateMessagingContext(string? connectionString = null)
        {
            var options = new DbContextOptionsBuilder<MessagingDbContext>()
                .UseNpgsql(connectionString ?? ConnectionString)
                .Options;

            return new MessagingDbContext(options);
        }

        public WebhookSourcesDbContext CreateWebhookSourcesContext(string? connectionString = null)
        {
            var options = new DbContextOptionsBuilder<WebhookSourcesDbContext>()
                .UseNpgsql(connectionString ?? ConnectionString)
                .Options;

            return new WebhookSourcesDbContext(options, new UnavailableAuditTrail());
        }

        public CustomersDbContext CreateCustomersContext(string? connectionString = null)
        {
            var options = new DbContextOptionsBuilder<CustomersDbContext>()
                .UseNpgsql(connectionString ?? ConnectionString)
                .Options;

            return new CustomersDbContext(options, new UnavailableAuditTrail());
        }

        public AuditDbContext CreateAuditContext(string? connectionString = null)
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseNpgsql(connectionString ?? ConnectionString)
                .Options;

            return new AuditDbContext(options);
        }
    }

    [CollectionDefinition(Name)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
    {
        public const string Name = "Postgres";
    }
}