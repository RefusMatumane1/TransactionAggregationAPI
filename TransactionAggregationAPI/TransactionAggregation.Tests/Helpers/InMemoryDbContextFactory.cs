using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.Audit.Infrastructure.Persistence;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources.Infrastructure.Persistence;

namespace TransactionAggregation.Tests.Helpers
{
    internal static class InMemoryDb
    {
        public static TContext Create<TContext>(string? dbName = null) where TContext : DbContext =>
            (TContext)Activator.CreateInstance(typeof(TContext), Options<TContext>(dbName))!;

        public static DbContextOptions<TContext> Options<TContext>(string? dbName) where TContext : DbContext =>
            new DbContextOptionsBuilder<TContext>()
                .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
                .Options;
    }

    public static class InMemoryDbContextFactory
    {
        public static TransactionsDbContext Create(
            string? dbName = null, MessagingDbContext? messagingDbContext = null, IAuditTrail? auditTrail = null) =>
            new InMemoryTransactionsDbContext(
                InMemoryDb.Options<TransactionsDbContext>(dbName),
                messagingDbContext ?? InMemoryMessagingDbContextFactory.Create(), auditTrail ?? new RecordingAuditTrail());

        // Audit events the context has committed, when it was created with the default RecordingAuditTrail.
        public static List<AuditEventRecord> RecordedAudit(this TransactionsDbContext context) =>
            ((RecordingAuditTrail)((InMemoryTransactionsDbContext)context).AuditTrail).Recorded;
    }

    // The InMemory provider has no transactions, so the base unit of work saves the contexts and the
    // audit events one after another; the atomic commit itself is covered by the Postgres suites.
    internal sealed class InMemoryTransactionsDbContext(
        DbContextOptions<TransactionsDbContext> options, MessagingDbContext messaging, IAuditTrail auditTrail)
        : TransactionsDbContext(options, messaging, auditTrail)
    {
        public IAuditTrail AuditTrail { get; } = auditTrail;
    }

    public sealed class RecordingAuditTrail : IAuditTrail
    {
        public List<AuditEventRecord> Recorded { get; } = [];

        public Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default)
        {
            Recorded.AddRange(events);
            return Task.CompletedTask;
        }

        public Task RecordWithinAsync(
            IReadOnlyCollection<AuditEventRecord> events, System.Data.Common.DbTransaction transaction, CancellationToken cancellationToken = default) =>
            RecordAsync(events, cancellationToken);
    }

    public static class InMemoryMessagingDbContextFactory
    {
        public static MessagingDbContext Create(string? dbName = null) => InMemoryDb.Create<MessagingDbContext>(dbName);
    }

    public static class InMemoryAuditDbContextFactory
    {
        public static AuditDbContext Create(string? dbName = null) => InMemoryDb.Create<AuditDbContext>(dbName);
    }

    public static class InMemoryWebhookSourcesDbContextFactory
    {
        public static WebhookSourcesDbContext Create(string? dbName = null, IAuditTrail? auditTrail = null) =>
            new(InMemoryDb.Options<WebhookSourcesDbContext>(dbName), auditTrail ?? new RecordingAuditTrail());
    }
}