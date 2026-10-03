using FluentAssertions;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Exceptions;
using System.Reflection;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain
{
    public class TransactionEntityTests
    {
        private static readonly DateTime BookedAt = new(2026, 9, 3, 8, 30, 0, DateTimeKind.Utc);

        [Fact]
        public void Record_CreatesABookedLedgerEntry_WithEveryFactAsGiven()
        {
            var tx = Transaction.Record("acc-9", Money.Create(-112.50m, "ZAR"), "Woolworths Sandton",
                TransactionCategory.Groceries, TransactionSource.Create("FNB", "bank-tx-1"), BookedAt,
                new Dictionary<string, string> { ["bankCategory"] = "Food" });

            tx.Status.Should().Be(TransactionStatus.Booked);
            tx.ExternalAccountId.Should().Be("acc-9");
            tx.Amount.Should().Be(Money.Create(-112.50m, "ZAR"));
            tx.Description.Should().Be("Woolworths Sandton");
            tx.Category.Should().Be(TransactionCategory.Groceries);
            tx.Source.Name.Should().Be("FNB");
            tx.Source.ExternalId.Should().Be("bank-tx-1");
            tx.Date.Should().Be(BookedAt);
            tx.Metadata.Should().ContainKey("bankCategory").WhoseValue.Should().Be("Food");
        }

        [Fact]
        public void Record_CopiesTheMetadata_SoTheCallersDictionaryCannotChangeTheEntry()
        {
            var metadata = new Dictionary<string, string> { ["k"] = "v" };
            var tx = TestTransactions.Create(-10m, metadata: metadata);

            metadata["k"] = "changed";

            tx.Metadata["k"].Should().Be("v");
        }

        [Fact]
        public void Record_RejectsANonUtcBookingDate()
        {
            var act = () => TestTransactions.Create(-10m, date: DateTime.SpecifyKind(BookedAt, DateTimeKind.Unspecified));

            act.Should().Throw<DomainException>().WithMessage("*UTC*");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Record_RejectsAnEmptyDescription(string description)
        {
            var act = () => TestTransactions.Create(-10m, description);

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void IsExpense_And_IsIncome_FollowTheSignOfTheAmount()
        {
            TestTransactions.Create(-100m).IsExpense.Should().BeTrue();
            TestTransactions.Create(500m).IsIncome.Should().BeTrue();
        }

        [Fact]
        public void ALedgerEntry_ExposesNoWayToChangeItAfterRecording()
        {
            var publicSetters = typeof(Transaction)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true })
                .Select(p => p.Name);
            var mutators = typeof(Transaction)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.ReturnType == typeof(void))
                .Select(m => m.Name);

            publicSetters.Should().BeEmpty();
            mutators.Should().BeEmpty("a recorded transaction is never edited");
            typeof(Transaction).GetProperty(nameof(Transaction.Metadata))!.PropertyType
                .Should().Be<IReadOnlyDictionary<string, string>>();
        }
    }
}