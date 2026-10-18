using FluentAssertions;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands
{
    public class ReceiveBankTransactionsCommandValidatorTests
    {
        private readonly ReceiveBankTransactionsCommandValidator _validator = new(Options.Create(new IngestionOptions()), TestCurrencies.Catalog);

        private static ExternalTransactionDTO MakeDto(string id) => new()
        {
            Id = id,
            Amount = -10m,
            Currency = "ZAR",
            Description = "test",
            Category = string.Empty,
            Date = DateTime.UtcNow
        };

        [Fact]
        public void Validate_UpToFiveHundredTransactions_IsValid()
        {
            var transactions = Enumerable.Range(1, 500).Select(i => MakeDto($"txn-{i}")).ToList();
            var command = new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, transactions);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_MoreThanFiveHundredTransactions_IsInvalid()
        {
            var transactions = Enumerable.Range(1, 501).Select(i => MakeDto($"txn-{i}")).ToList();
            var command = new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, transactions);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Transactions");
        }

        [Theory]
        [InlineData(10, 10, true)]
        [InlineData(10, 11, false)]
        [InlineData(1_000, 1_000, true)]
        [InlineData(1_000, 1_001, false)]
        public void Validate_UsesTheConfiguredLimit(int limit, int count, bool valid)
        {
            var validator = new ReceiveBankTransactionsCommandValidator(
                Options.Create(new IngestionOptions { MaxTransactionsPerDelivery = limit }), TestCurrencies.Catalog);
            var transactions = Enumerable.Range(1, count).Select(i => MakeDto($"txn-{i}")).ToList();

            var result = validator.Validate(new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, transactions));

            result.IsValid.Should().Be(valid);
            if (!valid)
                result.Errors.Should().Contain(e => e.ErrorMessage.Contains($"at most {limit} transactions"));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(1, true)]
        [InlineData(IngestionOptions.MaxAllowedTransactionsPerDelivery, true)]
        [InlineData(IngestionOptions.MaxAllowedTransactionsPerDelivery + 1, false)]
        public void Options_RejectLimitsOutsideTheSupportedRange(int limit, bool valid) =>
            IngestionOptions.IsValid(new IngestionOptions { MaxTransactionsPerDelivery = limit }).Should().Be(valid);
    }
}