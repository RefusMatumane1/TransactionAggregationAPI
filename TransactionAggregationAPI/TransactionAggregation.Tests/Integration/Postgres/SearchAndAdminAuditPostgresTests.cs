using BuildingBlocks.Persistence.Pagination;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources.Domain;
using Modules.WebhookSources.Infrastructure.Persistence;
using Npgsql;
using System.Data.Common;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class SearchAndAdminAuditPostgresTests(PostgresContainerFixture fixture)
    {
        private async Task<string> SeedAsync(params string[] descriptions)
        {
            var bank = $"search-{Guid.NewGuid():N}";
            using var context = fixture.CreateContext();
            foreach (var description in descriptions)
                context.Transactions.Add(TestTransactions.Create(-1m, description, institution: bank, account: "acc-1"));
            await context.SaveChangesAsync();
            return bank;
        }

        private async Task<List<string>> SearchAsync(string bank, string term)
        {
            using var context = fixture.CreateContext();
            var page = (await new GetTransactionsQueryHandler(context, new PostgresTransactionSearch(), new RowValueKeysetPaginator())
                .Handle(new GetTransactionsQuery(TestFilters.For(bank)) { SearchTerm = term, PageSize = 100 }, CancellationToken.None)).Value;
            return page.Items.Select(i => i.Description).ToList();
        }

        [Fact]
        public async Task Search_IsCaseInsensitive_OverTheDescription()
        {
            var bank = await SeedAsync("Woolworths Food", "Engen fuel");

            (await SearchAsync(bank, "WOOL")).Should().Equal("Woolworths Food");
        }

        [Theory]
        [InlineData("100%", "Refund 100% of fee")]
        [InlineData("a_b", "Ref a_b")]
        [InlineData(@"c:\x", @"Path c:\x")]
        public async Task Search_MatchesWildcardCharactersLiterally(string term, string expected)
        {
            var bank = await SeedAsync("Refund 100% of fee", "Refund 1000 of fee", "Ref a_b", "Ref axb", @"Path c:\x", "Path c:x");

            (await SearchAsync(bank, term)).Should().Equal(expected);
        }

        [Fact]
        public async Task Search_TreatsSqlAsText_AndLeavesTheLedgerIntact()
        {
            var bank = await SeedAsync("Coffee");

            (await SearchAsync(bank, "'; DROP TABLE transactions.\"Transactions\"; --")).Should().BeEmpty();
            (await SearchAsync(bank, "' OR '1'='1")).Should().BeEmpty();
            (await SearchAsync(bank, "Coffee")).Should().Equal("Coffee");
        }

        private WebhookSourcesDbContext AdminContext(string database, IAuditTrail? auditTrail = null)
        {
            var messaging = fixture.CreateMessagingContext(database);
            var connection = (NpgsqlConnection)messaging.Database.GetDbConnection();
            var options = new DbContextOptionsBuilder<WebhookSourcesDbContext>().UseNpgsql(connection).Options;
            return new WebhookSourcesDbContext(options, auditTrail ?? fixture.CreateAuditTrail(messaging));
        }

        [Fact]
        public async Task AnAdministrativeChange_CommitsTogetherWithItsAttributedAuditRow()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            var context = AdminContext(database);
            var (source, _) = WebhookSource.Create($"audited-{Guid.NewGuid():N}"[..20], "Audited bank", "#123456");
            var actor = Guid.NewGuid().ToString();

            context.WebhookSources.Add(source);
            context.StageAudit([new AuditEventRecord(Guid.NewGuid(), AuditEventTypes.SourceCreated, DateTime.UtcNow,
                AuditChannels.Admin, source.Name, Detail: "created", Actor: actor)]);
            await context.SaveChangesAsync();

            using var audit = fixture.CreateAuditContext(database);
            var recorded = await audit.AuditEvents.SingleAsync(e => e.SourceName == source.Name);
            recorded.EventType.Should().Be(AuditEventTypes.SourceCreated);
            recorded.Actor.Should().Be(actor);
            recorded.Channel.Should().Be(AuditChannels.Admin);
        }

        [Fact]
        public async Task AnAdministrativeChange_WhoseAuditRowCannotBeWritten_IsNotApplied()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            var context = AdminContext(database, new FailingAuditTrail());
            var (source, _) = WebhookSource.Create($"unaudited-{Guid.NewGuid():N}"[..20], "Unaudited bank", "#123456");

            context.WebhookSources.Add(source);
            context.StageAudit([new AuditEventRecord(Guid.NewGuid(), AuditEventTypes.SourceCreated, DateTime.UtcNow,
                AuditChannels.Admin, source.Name, Actor: Guid.NewGuid().ToString())]);
            var act = () => context.SaveChangesAsync();

            await act.Should().ThrowAsync<InvalidOperationException>();
            using var verify = fixture.CreateWebhookSourcesContext(database);
            (await verify.WebhookSources.AnyAsync(s => s.Name == source.Name))
                .Should().BeFalse("an admin change without its audit row would be unattributable");
        }

        private sealed class FailingAuditTrail : IAuditTrail
        {
            public Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("audit store unavailable");

            public Task RecordWithinAsync(IReadOnlyCollection<AuditEventRecord> events, DbTransaction transaction, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("audit store unavailable");
        }
    }
}