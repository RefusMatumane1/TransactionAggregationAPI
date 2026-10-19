using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Querying;
using BuildingBlocks.Persistence.Pagination;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Customers.Domain;
using Modules.Reporting.Application.Features.Aggregates;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Contracts;
using Modules.Transactions.Infrastructure.Persistence;
using Npgsql;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class CustomerPostgresTests(PostgresContainerFixture fixture)
    {
        private static DateTime Utc(int day, int hour) => new(2026, 6, day, hour, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task ACustomerFilter_SelectsExactlyItsAccounts_FromTheLedgerAndTheReadModel()
        {
            var db = await fixture.CreateIsolatedDatabaseAsync();
            var bankA = $"ca-{Guid.NewGuid():N}"[..20];
            var bankB = $"cb-{Guid.NewGuid():N}"[..20];
            using (var seed = fixture.CreateContext(connectionString: db))
            {
                seed.Transactions.AddRange(
                    TestTransactions.Create(-100m, "Mine at A", TransactionCategory.Groceries, institution: bankA, account: "shared-id", date: Utc(3, 9)),
                    TestTransactions.Create(-40m, "Mine at B", TransactionCategory.Dining, institution: bankB, account: "acc-b", date: Utc(4, 9)),
                    TestTransactions.Create(2500m, "Salary at B", TransactionCategory.Income, institution: bankB, account: "acc-b", date: Utc(5, 9)),
                    TestTransactions.Create(-999m, "Not mine", TransactionCategory.Shopping, institution: bankB, account: "shared-id", date: Utc(3, 9)));
                await seed.SaveChangesAsync();
            }
            await fixture.RefreshDailyTotalsAsync(db);

            var filter = new AccountFilter(InstitutionAccess.All, Accounts:
                [new AccountKey(bankA, "shared-id"), new AccountKey(bankB, "acc-b")]);

            using var context = fixture.CreateContext(connectionString: db);
            var list = (await new GetTransactionsQueryHandler(context, new PostgresTransactionSearch(), new RowValueKeysetPaginator(), TestCurrencies.Catalog)
                .Handle(new GetTransactionsQuery(filter) { PageSize = 50 }, CancellationToken.None)).Value;
            using var reporting = fixture.CreateReportingContext(connectionString: db);
            var cashFlow = (await new GetCashFlowQueryHandler(reporting, TestCurrencies.Catalog)
                .Handle(new GetCashFlowQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), filter), CancellationToken.None)).Value;
            var nothing = (await new GetCashFlowQueryHandler(reporting, TestCurrencies.Catalog)
                .Handle(new GetCashFlowQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
                    new AccountFilter(InstitutionAccess.All, Accounts: [])), CancellationToken.None)).Value;

            list.Items.Select(t => t.Description).Should().BeEquivalentTo(["Mine at A", "Mine at B", "Salary at B"]);
            cashFlow.TotalExpenses.Should().Be(140m);
            cashFlow.TotalIncome.Should().Be(2500m);
            nothing.Points.Sum(p => p.TransactionCount).Should().Be(0, "a customer with no visible accounts matches nothing");
        }

        [Fact]
        public async Task TheDatabase_RefusesADuplicateReference_ADuplicateLink_AndAMalformedCode()
        {
            var db = await fixture.CreateIsolatedDatabaseAsync();
            var customer = Customer.Create("CUST-1", "Thandi Nkosi");
            customer.Link(TestInstitutions.FNB, "62001", DateTime.UtcNow);
            using (var context = fixture.CreateCustomersContext(db))
            {
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
            }

            await using var connection = new NpgsqlConnection(db);
            await connection.OpenAsync();
            async Task<string?> FailureOf(string sql)
            {
                try
                {
                    await using var command = new NpgsqlCommand(sql, connection);
                    await command.ExecuteNonQueryAsync();
                    return null;
                }
                catch (PostgresException ex)
                {
                    return ex.SqlState;
                }
            }

            var id = customer.Id.Value;
            var duplicateReference = await FailureOf(
                $"INSERT INTO customerdirectory.\"Customers\" VALUES ('{Guid.NewGuid()}', 'CUST-1', 'Someone', now(), NULL)");
            var duplicateLink = await FailureOf(
                $"INSERT INTO customerdirectory.\"CustomerAccounts\" (\"CustomerId\", \"Institution\", \"ExternalAccountId\", \"LinkedAt\") VALUES ('{id}', 'FNB', '62001', now())");
            var malformedReference = await FailureOf(
                $"INSERT INTO customerdirectory.\"Customers\" VALUES ('{Guid.NewGuid()}', 'has space', 'Someone', now(), NULL)");
            var blankAccount = await FailureOf(
                $"INSERT INTO customerdirectory.\"CustomerAccounts\" (\"CustomerId\", \"Institution\", \"ExternalAccountId\", \"LinkedAt\") VALUES ('{id}', 'FNB', '  ', now())");

            duplicateReference.Should().Be(PostgresErrorCodes.UniqueViolation);
            duplicateLink.Should().Be(PostgresErrorCodes.UniqueViolation);
            malformedReference.Should().Be(PostgresErrorCodes.CheckViolation);
            blankAccount.Should().Be(PostgresErrorCodes.CheckViolation);
        }

        [Fact]
        public async Task LinksRoundTrip_ThroughTheAggregate()
        {
            var db = await fixture.CreateIsolatedDatabaseAsync();
            var customer = Customer.Create("CUST-2", "Johan Botha");
            customer.Link(TestInstitutions.FNB, "62002", DateTime.UtcNow);
            customer.Link(TestInstitutions.Capitec, "13002", DateTime.UtcNow);
            using (var context = fixture.CreateCustomersContext(db))
            {
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
            }

            using (var context = fixture.CreateCustomersContext(db))
            {
                var loaded = await context.Customers.SingleAsync(c => c.Reference == "CUST-2");
                loaded.Unlink(TestInstitutions.FNB, "62002").Should().BeTrue();
                await context.SaveChangesAsync();
            }

            using var verify = fixture.CreateCustomersContext(db);
            (await verify.Customers.SingleAsync(c => c.Reference == "CUST-2")).Accounts
                .Select(a => (a.Institution, a.ExternalAccountId)).Should().Equal((TestInstitutions.Capitec, "13002"));
        }
    }
}