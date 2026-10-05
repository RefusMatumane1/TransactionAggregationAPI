using FluentAssertions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands
{
    public class InboundContractValidationTests
    {
        private static readonly ReceiveBankTransactionsCommandValidator Validator = new();

        private static ReceiveBankTransactionsCommand Command(decimal amount = -10m, string currency = "ZAR", int schemaVersion = BankTransactionsMessage.CurrentSchemaVersion, string? institution = null) =>
            new("source", "ext-acc", institution, [new ExternalTransactionDTO
            {
                Id = "t1", Amount = amount, Currency = currency, Description = "d", Category = string.Empty,
                Date = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            }], SchemaVersion: schemaVersion);

        [Fact]
        public void SupportedSchemaVersion_IsAccepted() =>
            Validator.Validate(Command(schemaVersion: BankTransactionsMessage.CurrentSchemaVersion)).IsValid.Should().BeTrue();

        [Fact]
        public void SchemaVersionOne_IsStillAccepted_BecauseTheBankComesFromTheKey() =>
            Validator.Validate(Command(schemaVersion: 1)).IsValid.Should().BeTrue();

        [Fact]
        public void UnsupportedSchemaVersion_IsRejected_NamingTheSupportedOnes()
        {
            var result = Validator.Validate(Command(schemaVersion: 3));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ReceiveBankTransactionsCommand.SchemaVersion))
                .Which.ErrorMessage.Should().Contain("supported: 1, 2");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("FNB")]
        public void Institution_IsOptional(string? institution) =>
            Validator.Validate(Command(institution: institution)).IsValid.Should().BeTrue();

        [Fact]
        public void Institution_WhenSent_MustBePrintable()
        {
            var result = Validator.Validate(Command(institution: "FNB\n"));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(ReceiveBankTransactionsCommand.Institution));
        }

        [Theory]
        [InlineData(-1.125, true)]
        [InlineData(-1.1234, true)]
        [InlineData(-1.12345, false)]
        public void AmountScale_IsLimitedToWhatTheColumnStores(double amount, bool valid) =>
            Validator.Validate(Command(amount: (decimal)amount)).IsValid.Should().Be(valid);

        [Theory]
        [InlineData("ZAR", true)]
        [InlineData("zar", true)]
        [InlineData("KWD", true)]
        [InlineData("USD", true)]
        [InlineData("ZA", false)]
        [InlineData("XAU", false)]
        [InlineData("ABC", false)]
        public void Currency_MustBeAnIso4217AccountCurrency(string currency, bool valid) =>
            Validator.Validate(Command(currency: currency)).IsValid.Should().Be(valid);
    }
}