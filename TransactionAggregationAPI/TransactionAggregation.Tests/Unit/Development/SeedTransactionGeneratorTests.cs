using FluentAssertions;
using SharedKernel.Common.ValueObjects;
using TransactionAggregationAPI.Development;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Development
{
    public class SeedTransactionGeneratorTests
    {
        private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        private static SeedAccount Account(SeedAccountType type = SeedAccountType.Checking) =>
            new("ZA0010000001", "FNB", type, 5, SeedCatalog.Households[0].Profile);

        private static SeedTransactionGenerator Generator(int seed, Guid? runId = null) =>
            new(new Random(seed), runId ?? Guid.NewGuid(), Now);

        [Theory]
        [InlineData(SeedAccountType.Checking)]
        [InlineData(SeedAccountType.Savings)]
        [InlineData(SeedAccountType.CreditCard)]
        [InlineData(SeedAccountType.Investment)]
        public void FirstRun_BackfillsFifteenMonthsOfHistory_EndingNow(SeedAccountType type)
        {
            var transactions = Generator(1).Generate(Account(type), lastSeededUtc: null);

            var earliestAllowed = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            transactions.Should().OnlyContain(t => t.Date >= earliestAllowed && t.Date <= Now);
            transactions.Select(t => (t.Date.Year, t.Date.Month)).Distinct().Should().HaveCount(SeedTransactionGenerator.HistoryMonths);
            transactions.Should().OnlyContain(t => t.Amount.Currency == SupportedCurrency.Default);
        }

        [Fact]
        public void Restart_AddsOnlyWhatHappenedSinceTheLastSeed_NeverInTheFuture()
        {
            var lastSeeded = Now.AddDays(-40);

            var transactions = Generator(2).Generate(Account(), lastSeeded);

            transactions.Should().NotBeEmpty();
            transactions.Should().OnlyContain(t => t.Date > lastSeeded && t.Date <= Now);
        }

        [Fact]
        public void Restart_MomentsAfterThePreviousRun_StillAddsFreshActivity()
        {
            var transactions = Generator(3).Generate(Account(), Now.AddMinutes(-1));

            transactions.Count.Should().BeGreaterThanOrEqualTo(2);
            transactions.Should().OnlyContain(t => t.Date > Now.AddMinutes(-1) && t.Date <= Now);
        }

        [Fact]
        public void DifferentRuns_ProduceDifferentData_ButAPinnedSeedIsReproducible()
        {
            var account = Account();
            var runId = Guid.NewGuid();

            static string Fingerprint(IEnumerable<Modules.Transactions.Domain.Entities.Transaction> transactions) =>
                string.Join('|', transactions.Select(t => $"{t.Date:O}:{t.Amount.Amount}:{t.Description}"));

            var first = Fingerprint(Generator(10, runId).Generate(account, null));
            var sameSeed = Fingerprint(Generator(10, runId).Generate(account, null));
            var otherSeed = Fingerprint(Generator(11, runId).Generate(account, null));

            sameSeed.Should().Be(first);
            otherSeed.Should().NotBe(first);
        }

        [Fact]
        public void ExternalIds_AreUniqueWithinAndAcrossRuns()
        {
            var account = Account(SeedAccountType.CreditCard);

            var ids = Generator(4).Generate(account, null).Concat(Generator(4).Generate(account, null))
                .Select(t => t.Source.ExternalId)
                .ToList();

            ids.Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void Catalog_AccountIdsAreUniqueAndEveryBankIsRepresented()
        {
            var accounts = SeedCatalog.Households.SelectMany(h => h.Accounts).ToList();

            accounts.Select(a => a.ExternalAccountId).Should().OnlyHaveUniqueItems();
            accounts.Select(a => a.Institution).Distinct().Should().BeEquivalentTo(["FNB", "StandardBank", "Absa", "Capitec"]);
        }
    }
}