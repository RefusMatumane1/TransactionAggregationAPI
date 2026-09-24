using FluentAssertions;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Transactions.Application.Features.Transactions.Queries.ExportTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Application.Mappings;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using System.Text;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

/// <summary>
/// Filtering, sorting, pagination and export — the brief's "support filtering, sorting and
/// pagination" requirement, pinned rule by rule rather than by a single unfiltered call.
/// </summary>
public class TransactionListAndExportTests
{
    private static readonly DateTime Day = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _customer = Guid.NewGuid();

    private static IMapper Mapper()
    {
        var config = new TypeAdapterConfig();
        new TransactionProfile().Register(config);
        return new Mapper(config);
    }

    private async Task<TransactionsDbContext> SeedAsync()
    {
        var context = InMemoryDbContextFactory.Create();
        var customerId = CustomerId.CreateFrom(_customer);

        Transaction Tx(decimal amount, string description, TransactionCategory category, string source, int dayOffset, bool settled = true)
        {
            var t = Transaction.Create(customerId, Money.Create(amount, "ZAR"), description, category,
                TransactionSource.Create(source, Guid.NewGuid().ToString("N")), date: Day.AddDays(dayOffset));
            return settled ? t.Settled() : t;
        }

        context.Transactions.AddRange(
            Tx(-50m, "Woolworths Food", TransactionCategory.Groceries, "FNB", 0),
            Tx(-500m, "Engen fuel", TransactionCategory.Transportation, "FNB", 1),
            Tx(2000m, "Salary ACME", TransactionCategory.Income, "Absa", 2),
            Tx(-20m, "Woolies coffee", TransactionCategory.Groceries, "Absa", 3, settled: false));

        // Another customer's row must never leak into any result.
        context.Transactions.Add(Transaction.Create(CustomerId.Create(), Money.Create(-1m, "ZAR"), "Woolworths other",
            TransactionCategory.Groceries, TransactionSource.Create("FNB", "x"), date: Day));

        await context.SaveChangesAsync();
        return context;
    }

    private async Task<IReadOnlyList<string>> ListAsync(Func<GetTransactionsQuery, GetTransactionsQuery> configure)
    {
        var context = await SeedAsync();
        var handler = new GetTransactionsQueryHandler(context, Mapper(), NullLogger<GetTransactionsQueryHandler>.Instance);

        var result = await handler.Handle(configure(new GetTransactionsQuery { CustomerId = _customer, PageSize = 100 }), CancellationToken.None);

        return result.Value.Items.Select(i => i.Description).ToList();
    }

    [Fact]
    public async Task Filter_ByCategory() =>
        (await ListAsync(q => q with { Category = TransactionCategory.Groceries }))
            .Should().BeEquivalentTo(["Woolworths Food", "Woolies coffee"]);

    [Fact]
    public async Task Filter_ByStatus() =>
        (await ListAsync(q => q with { Status = TransactionStatus.Pending }))
            .Should().BeEquivalentTo(["Woolies coffee"]);

    [Fact]
    public async Task Filter_ByDateRange_IsInclusiveAtBothEnds() =>
        (await ListAsync(q => q with { FromDate = Day.AddDays(1), ToDate = Day.AddDays(2) }))
            .Should().BeEquivalentTo(["Engen fuel", "Salary ACME"]);

    [Fact]
    public async Task Filter_ByAmount_ComparesMagnitude_SoDebitsAndCreditsBothMatch() =>
        (await ListAsync(q => q with { MinAmount = 50m, MaxAmount = 500m }))
            .Should().BeEquivalentTo(["Woolworths Food", "Engen fuel"]);

    [Fact]
    public async Task Filter_BySearchTerm_IsCaseInsensitiveOverDescription() =>
        (await ListAsync(q => q with { SearchTerm = "WOOL" }))
            .Should().BeEquivalentTo(["Woolworths Food", "Woolies coffee"]);

    [Fact]
    public async Task Filter_BySource() =>
        (await ListAsync(q => q with { Source = "Absa" }))
            .Should().BeEquivalentTo(["Salary ACME", "Woolies coffee"]);

    [Fact]
    public async Task Sort_DefaultsToNewestFirst() =>
        (await ListAsync(q => q))
            .Should().ContainInOrder("Woolies coffee", "Salary ACME", "Engen fuel", "Woolworths Food");

    [Fact]
    public async Task Sort_ByAmountAscending() =>
        (await ListAsync(q => q with { SortBy = "amount", SortDescending = false }))
            .Should().ContainInOrder("Engen fuel", "Woolworths Food", "Woolies coffee", "Salary ACME");

    [Fact]
    public async Task Sort_UnknownKey_FallsBackToDate_NotAnError() =>
        (await ListAsync(q => q with { SortBy = "drop table", SortDescending = false }))
            .Should().ContainInOrder("Woolworths Food", "Engen fuel", "Salary ACME", "Woolies coffee");

    [Fact]
    public async Task Paging_ReturnsTheRequestedSlice_AndTheTotalCount()
    {
        var context = await SeedAsync();
        var handler = new GetTransactionsQueryHandler(context, Mapper(), NullLogger<GetTransactionsQueryHandler>.Instance);

        var page2 = await handler.Handle(
            new GetTransactionsQuery { CustomerId = _customer, PageNumber = 2, PageSize = 3, SortDescending = false },
            CancellationToken.None);

        page2.Value.TotalCount.Should().Be(4, "only this customer's rows count");
        page2.Value.Items.Select(i => i.Description).Should().Equal("Woolies coffee");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validator_RejectsPageSizesOutsideTheBound(int pageSize) =>
        new GetTransactionsQueryValidator()
            .Validate(new GetTransactionsQuery { CustomerId = _customer, PageSize = pageSize })
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Export_WritesOnlyTheCustomersRows_WithFullPrecisionAmounts()
    {
        var context = await SeedAsync();
        context.Transactions.Add(Transaction.Create(CustomerId.CreateFrom(_customer), Money.Create(-1.125m, "KWD"),
            "Kuwait", TransactionCategory.Shopping, TransactionSource.Create("FNB", "kwd-1"), date: Day));
        await context.SaveChangesAsync();
        var handler = new ExportTransactionsQueryHandler(context, NullLogger<ExportTransactionsQueryHandler>.Instance);

        var result = await handler.Handle(new ExportTransactionsQuery { CustomerId = _customer }, CancellationToken.None);

        result.Value.RecordCount.Should().Be(5);
        var csv = Encoding.UTF8.GetString(result.Value.Content);
        csv.Should().Contain("-1.125").And.NotContain("Woolworths other");
    }

    [Fact]
    public async Task Export_AboveTheRowCap_IsRefusedWithAFieldError_NotLoadedIntoMemory()
    {
        var context = await SeedAsync();
        var handler = new ExportTransactionsQueryHandler(context, NullLogger<ExportTransactionsQueryHandler>.Instance, maxExportRows: 3);

        var result = await handler.Handle(new ExportTransactionsQuery { CustomerId = _customer }, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<FieldValidationError>()
            .Which.Errors.Should().ContainKey("fromDate");
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("+27 transfer", "\"'+27 transfer\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1)\"")]
    [InlineData("Plain shop", "\"Plain shop\"")]
    public void Export_Quote_NeutralisesSpreadsheetFormulas(string value, string expected) =>
        ExportTransactionsQueryHandler.Quote(value).Should().Be(expected);
}