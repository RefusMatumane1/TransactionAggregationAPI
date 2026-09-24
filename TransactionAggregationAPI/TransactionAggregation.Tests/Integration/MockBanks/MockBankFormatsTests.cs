using FluentAssertions;
using Modules.BankLinks.Domain.ValueObjects;
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

namespace TransactionAggregation.Tests.Integration.MockBanks;

/// <summary>
/// The seam between the mock banks and the application: each mock bank's output is put on
/// the wire, read back through the application's own BankTransactionsMessage contract, and
/// normalized with the rules Development actually loads. If a mock format or a Development
/// rule changes without the other, these fail instead of the dev feed quietly degrading.
/// </summary>
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
        var json = JsonSerializer.Serialize(new DeliveryPayload("acc-1", [style.Render(transaction)]), JsonSerializerOptions.Web);
        var message = JsonSerializer.Deserialize<BankTransactionsMessage>(json, JsonSerializerOptions.Web)!;
        return (normalizer ?? DevelopmentNormalizer).Normalize(message.ToExternalTransactionDtos().Single(), style.Institution);
    }

    public static TheoryData<string, string, string, string, TransactionCategory?> Cases => new()
    {
        // bank, merchant, raw description the bank sends, normalized description, category hint
        { "FNB", "Woolworths", "POS PURCHASE  WOOLWORTHS  SANDTON", "WOOLWORTHS SANDTON", TransactionCategory.Groceries },
        { "FNB", "Nandos", "POS PURCHASE  NANDOS  FOURWAYS", "NANDOS FOURWAYS", TransactionCategory.Dining },
        { "FNB", "Engen", "POS PURCHASE  ENGEN  MIDRAND", "ENGEN MIDRAND", TransactionCategory.Transportation },
        { "FNB", "City Power", "DEBIT ORDER  CITY POWER", "CITY POWER", TransactionCategory.Utilities },
        { "Absa", "Woolworths", "ABSA CARD Woolworths Sandton", "Woolworths Sandton", null },
        { "Absa", "Netflix", "DEBIT ORDER Netflix", "Netflix", null },
        { "Capitec", "Woolworths", "Woolworths Sandton", "Woolworths Sandton", TransactionCategory.Groceries },
        { "Capitec", "Netflix", "DEBIT ORDER Netflix", "Netflix", TransactionCategory.Subscriptions },
        // "Other": no profile, so its own prefix is left alone — only the defaults apply.
        { "StandardBank", "Woolworths", "PURCHASE Woolworths", "PURCHASE Woolworths", null },
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
    [InlineData("FNB")]          // local time, no offset
    [InlineData("Absa")]         // explicit +02:00 offset
    [InlineData("Capitec")]      // UTC
    [InlineData("StandardBank")] // local time, no offset, no profile
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
        // Without the Development overlay, the invented "ABSA CARD" prefix stays: the mock's
        // formats must never leak into the rules real environments load.
        var result = ThroughTheApplication(new AbsaStyle(), Tx("Woolworths"), TestNormalizers.Shipped());

        result.Description.Should().Be("ABSA CARD Woolworths Sandton");
    }

    [Fact]
    public void EveryMockInstitution_IsARealInstitutionWithAStyle()
    {
        foreach (var account in MockCatalog.Accounts)
        {
            Enum.TryParse<Institution>(account.Institution, out _).Should().BeTrue(
                $"'{account.Institution}' must be a name BankLinks sends in the authorize request");
            BankStatementStyles.ByInstitution.Should().ContainKey(account.Institution);
        }
    }
}