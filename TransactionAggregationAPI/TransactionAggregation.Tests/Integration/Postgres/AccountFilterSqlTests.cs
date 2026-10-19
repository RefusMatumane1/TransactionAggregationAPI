using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Querying;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Queries;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// The account filter is built as an expression tree by hand. These pin what matters about the SQL it becomes: the caller's
    /// values travel as parameters, so the statement text, and the plan cached for it, does not vary with who is asking.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class AccountFilterSqlTests(PostgresContainerFixture fixture)
    {
        private string LedgerSql(AccountFilter filter)
        {
            using var context = fixture.CreateContext();
            var query = context.Transactions.Ledger(filter).ToQueryString();

            return string.Join('\n', query.Split('\n').Where(line => !line.StartsWith("--")));
        }

        private static AccountFilter For(string bank, string account, string otherBank, string otherAccount) =>
            new(InstitutionAccess.Only([bank, otherBank]), bank, account, [new AccountKey(bank, account), new AccountKey(otherBank, otherAccount)]);

        [Fact]
        public void TheCallersValues_AreParametersNeverPartOfTheStatement()
        {
            var sql = LedgerSql(For("FNB", "acct-7", "Absa", "acct-9"));

            sql.Should().NotContain("FNB").And.NotContain("Absa").And.NotContain("acct-7").And.NotContain("acct-9");
            sql.Should().Contain("@");
        }

        [Fact]
        public void TwoDifferentCallers_ShareOneStatementText() =>
            LedgerSql(For("FNB", "acct-7", "Absa", "acct-9"))
                .Should().Be(LedgerSql(For("Capitec", "other-1", "StandardBank", "other-2")));

        [Fact]
        public void ACallerWhoMayReadEverything_AddsNoInstitutionRestriction()
        {
            using var context = fixture.CreateContext();

            var all = context.Transactions.Ledger(new AccountFilter(InstitutionAccess.All)).ToQueryString();
            var restricted = context.Transactions.Ledger(new AccountFilter(InstitutionAccess.Only(["FNB"]))).ToQueryString();

            restricted.Length.Should().BeGreaterThan(all.Length);
        }
    }
}
