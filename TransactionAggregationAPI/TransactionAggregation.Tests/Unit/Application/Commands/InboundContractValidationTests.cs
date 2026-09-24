using FluentAssertions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

/// <summary>The versioned inbound contract and the value rules that protect stored amounts.</summary>
public class InboundContractValidationTests
{
    private static readonly ReceiveBankTransactionsCommandValidator Validator = new();

    private static ReceiveBankTransactionsCommand Command(decimal amount = -10m, string currency = "ZAR", int schemaVersion = 1) =>
        new("source", "ext-acc", [new ExternalTransactionDTO
        {
            Id = "t1", Amount = amount, Currency = currency, Description = "d", Category = string.Empty,
            Date = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        }], SchemaVersion: schemaVersion);

    [Fact]
    public void SupportedSchemaVersion_IsAccepted() =>
        Validator.Validate(Command(schemaVersion: BankTransactionsMessage.CurrentSchemaVersion)).IsValid.Should().BeTrue();

    [Fact]
    public void UnsupportedSchemaVersion_IsRejected_NamingTheSupportedOnes()
    {
        var result = Validator.Validate(Command(schemaVersion: 2));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ReceiveBankTransactionsCommand.SchemaVersion))
            .Which.ErrorMessage.Should().Contain("supported: 1");
    }

    [Theory]
    [InlineData(-1.125, true)]
    [InlineData(-1.1234, true)]
    [InlineData(-1.12345, false)]
    public void AmountScale_IsLimitedToWhatTheColumnStores(double amount, bool valid) =>
        Validator.Validate(Command(amount: (decimal)amount)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData("ZAR", true)]
    [InlineData("kwd", true)]
    [InlineData("ZA1", false)]
    [InlineData("ZA", false)]
    public void Currency_MustBeAThreeLetterCode(string currency, bool valid) =>
        Validator.Validate(Command(currency: currency)).IsValid.Should().Be(valid);
}