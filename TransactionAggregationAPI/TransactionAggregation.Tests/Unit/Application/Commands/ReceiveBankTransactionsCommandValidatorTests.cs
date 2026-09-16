using FluentAssertions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class ReceiveBankTransactionsCommandValidatorTests
{
    private readonly ReceiveBankTransactionsCommandValidator _validator = new();

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
        var command = new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", transactions);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MoreThanFiveHundredTransactions_IsInvalid()
    {
        var transactions = Enumerable.Range(1, 501).Select(i => MakeDto($"txn-{i}")).ToList();
        var command = new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", transactions);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Transactions");
    }
}
