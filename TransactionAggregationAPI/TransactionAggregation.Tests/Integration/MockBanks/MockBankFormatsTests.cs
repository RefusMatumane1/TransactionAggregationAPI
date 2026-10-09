using FluentAssertions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Domain.Enums;
using System.Text.Json;
using TransactionAggregation.MockAggregator.Banks;
using TransactionAggregation.MockAggregator.Catalog;
using TransactionAggregation.MockAggregator.Feed;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.MockBanks
{
    public class MockBankFormatsTests
    {
        private static readonly DateTime When = new(2026, 9, 10, 10, 30, 0, DateTimeKind.Utc);
        private static readonly ITransactionNormalizer DevelopmentNormalizer = TestNormalizers.ShippedFor("Development");

        private static Merchant MerchantNamed(string name) => MockCatalog.Merchants.Single(m => m.Name == name);

        private static GeneratedTransaction Tx(string merchant, bool pending = false) =>
            new($"tx-{Guid.NewGuid():N}", MerchantNamed(merchant), -123.45m, When, pending);

        private static NormalizedTransaction ThroughTheApplication(
            IBankStatementStyle style, GeneratedTransaction transaction, ITransactionNormalizer? normalizer = null)
        {
            var json = JsonSerializer.Serialize(new DeliveryPayload("acc-1", style.Institution, [style.Render(transaction)]), JsonSerializerOptions.Web);
            var message = JsonSerializer.Deserialize<BankTransactionsMessage>(json, JsonSerializerOptions.Web)!;
            return (normalizer ?? DevelopmentNormalizer).Normalize(message.ToExternalTransactionDtos().Single(), style.Institution);
        }

        public static TheoryData<string, string, string, string, TransactionCategory?> Cases => new()
        {
            { "FNB", "Woolworths", "POS PURCHASE  WOOLWORTHS  SANDTON", "WOOLWORTHS SANDTON", TransactionCategory.Groceries },
            { "FNB", "Nandos", "POS PURCHASE  NANDOS  FOURWAYS", "NANDOS FOURWAYS", TransactionCategory.Dining },
            { "FNB", "Engen", "POS PURCHASE  ENGEN  MIDRAND", "ENGEN MIDRAND", TransactionCategory.Transportation },
            { "FNB", "City Power", "DEBIT ORDER  CITY POWER", "CITY POWER", TransactionCategory.Utilities },
            { "Absa", "Woolworths", "ABSA CARD Woolworths Sandton", "Woolworths Sandton", null },
            { "Absa", "Netflix", "DEBIT ORDER Netflix", "Netflix", null },
            { "Capitec", "Woolworths", "Woolworths Sandton", "Woolworths Sandton", TransactionCategory.Groceries },
            { "Capitec", "Netflix", "DEBIT ORDER Netflix", "Netflix", TransactionCategory.Subscriptions },

            { "StandardBank", "Woolworths", "PURCHASE Woolworths", "Woolworths", null },
            { "StandardBank", "Netflix", "DEBIT ORDER Netflix", "Netflix", null },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        public void EachMockBank_IsNormalizedToTheCanonicalForm(
            string bank, string merchant, string rawDescription, string normalized, TransactionCategory? category)
        {
            var style = BankStatementStyles.ByInstitution[bank];
            var transaction = Tx(merchant);

            style.Render(transaction).Description.Should().Be(rawDescription);

            var result = ThroughTheApplication(style, transaction);
            result.Description.Should().Be(normalized);
            result.BankCategory.Should().Be(category);
            result.OriginalDescription.Should().Be(rawDescription == normalized ? null : rawDescription);
        }

        [Theory]
        [InlineData("FNB")]
        [InlineData("Absa")]
        [InlineData("Capitec")]
        [InlineData("StandardBank")]
        public void EveryDateFormat_ArrivesAsTheSameUtcInstant(string bank)
        {
            var result = ThroughTheApplication(BankStatementStyles.ByInstitution[bank], Tx("Woolworths"));

            result.DateUtc.Should().Be(When);
            result.DateUtc.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AmountAndStatus_SurviveTheRoundTrip(bool pending)
        {
            var result = ThroughTheApplication(new FnbStyle(), Tx("Checkers", pending));

            result.Amount.Should().Be(-123.45m);
            result.Currency.Should().Be("ZAR");
            result.IsPending.Should().Be(pending);
        }

        [Fact]
        public void MockBankRules_AreNotInTheShippedRulesFile()
        {
            var result = ThroughTheApplication(new AbsaStyle(), Tx("Woolworths"), TestNormalizers.Shipped());

            result.Description.Should().Be("ABSA CARD Woolworths Sandton");
        }

        [Fact]
        public void EveryMockInstitution_IsARegisteredBankWithAStyle()
        {
            foreach (var account in MockCatalog.Accounts)
            {
                TransactionAggregationAPI.Development.MockAggregatorSource.Banks.Select(b => b.Code).Should().Contain(account.Institution,
                    $"'{account.Institution}' must be a bank the API registers, or the mock has no key to deliver it with");
                BankStatementStyles.ByInstitution.Should().ContainKey(account.Institution);
            }
        }
    }
}