using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
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

            using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
                await _container.DisposeAsync();
        }

        public ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            var mediator = Substitute.For<IMediator>();
            mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new ApplicationDbContext(options, mediator);
        }
    }

    [CollectionDefinition(Name)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
    {
        public const string Name = "Postgres";
    }
}
