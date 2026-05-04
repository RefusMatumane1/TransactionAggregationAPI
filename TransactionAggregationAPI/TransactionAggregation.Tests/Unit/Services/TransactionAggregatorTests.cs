using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Options;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Infrastructure.Services;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Services;

public class TransactionAggregatorTests
{
    // A real categorization service wired with default keyword rules for test assertions
    private static readonly ITransactionCategorizationService CategorizationService =
        new TransactionCategorizationService(Options.Create(new CategorizationOptions
        {
            Keywords = new Dictionary<string, string>
            {
                ["grocery"] = "Groceries",
                ["salary"] = "Income",
                ["uber"] = "Transportation"
            }
        }));

    private static ExternalTransactionDTO MakeDto(string id, decimal amount, string description = "grocery store") =>
        new()
        {
            Id = id,
            Amount = amount,
            Currency = "ZAR",
            Description = description,
            Category = "",
            Date = DateTime.UtcNow.AddDays(-1)
        };

    private static ITransactionSource MockSource(string name, IReadOnlyList<ExternalTransactionDTO> data)
    {
        var source = Substitute.For<ITransactionSource>();
        source.SourceName.Returns(name);
        source.GetTransactionsAsync(
                Arg.Any<CustomerId>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
              .Returns(data);
        return source;
    }

    private static TransactionAggregator BuildAggregator(params ITransactionSource[] sources) =>
        new(sources, CategorizationService, NullLogger<TransactionAggregator>.Instance);

    [Fact]
    public async Task AggregateAsync_MergesResultsFromMultipleSources()
    {
        var source1 = MockSource("SourceA", new List<ExternalTransactionDTO> { MakeDto("a1", -100m), MakeDto("a2", 500m) });
        var source2 = MockSource("SourceB", new List<ExternalTransactionDTO> { MakeDto("b1", -200m) });

        var result = await BuildAggregator(source1, source2)
            .AggregateCustomerTransactionsAsync(Guid.NewGuid(), null, null);

        result.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task AggregateAsync_DeduplicatesByExternalId()
    {
        var source1 = MockSource("SourceA", new List<ExternalTransactionDTO> { MakeDto("duplicate-id", -100m) });
        var source2 = MockSource("SourceB", new List<ExternalTransactionDTO> { MakeDto("duplicate-id", -100m) });

        var result = await BuildAggregator(source1, source2)
            .AggregateCustomerTransactionsAsync(Guid.NewGuid(), null, null);

        result.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task AggregateAsync_WhenOneSourceFails_StillReturnsResultsFromOtherSources()
    {
        var goodSource = MockSource("Good", new List<ExternalTransactionDTO> { MakeDto("ok1", -50m) });

        var failSource = Substitute.For<ITransactionSource>();
        failSource.SourceName.Returns("Bad");
        failSource.GetTransactionsAsync(
                Arg.Any<CustomerId>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new HttpRequestException("Source unavailable"));

        var result = await BuildAggregator(goodSource, failSource)
            .AggregateCustomerTransactionsAsync(Guid.NewGuid(), null, null);

        result.Transactions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AggregateAsync_AutoCategorizesIncome()
    {
        var source = MockSource("Source", new List<ExternalTransactionDTO> { MakeDto("inc1", 2500m, "salary deposit") });

        var result = await BuildAggregator(source)
            .AggregateCustomerTransactionsAsync(Guid.NewGuid(), null, null);

        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].Category.Should().Be(TransactionCategory.Income);
    }

    [Fact]
    public async Task AggregateAsync_ReturnsEmptyWhenNoSources()
    {
        var result = await BuildAggregator()
            .AggregateCustomerTransactionsAsync(Guid.NewGuid(), null, null);

        result.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task AggregateAsync_RespectsDateRangeFromParameters()
    {
        var source = Substitute.For<ITransactionSource>();
        source.SourceName.Returns("DateSource");

        DateTime? capturedFrom = null;
        DateTime? capturedTo = null;

        source.GetTransactionsAsync(
                Arg.Any<CustomerId>(),
                Arg.Do<DateTime>(d => capturedFrom = d),
                Arg.Do<DateTime>(d => capturedTo = d),
                Arg.Any<CancellationToken>())
              .Returns(new List<ExternalTransactionDTO>());

        var aggregator = BuildAggregator(source);

        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = new DateTime(2025, 3, 31, 0, 0, 0, DateTimeKind.Utc);

        await aggregator.AggregateCustomerTransactionsAsync(Guid.NewGuid(), from, to);

        capturedFrom.Should().Be(from);
        capturedTo.Should().Be(to);
    }
}
