using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Currency.Contracts;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Application.DTOs;
using Modules.Reporting.Application.Persistence;
using Modules.Transactions.Contracts;
using SharedKernel.Common.Models;

namespace Modules.Reporting.Application.Features.Aggregates
{
    internal sealed class GetPeriodComparisonQueryHandler(IReportingDbContext context, ICurrencyCatalog catalog)
        : IQueryHandler<GetPeriodComparisonQuery, PeriodComparisonDto>
    {
        public async Task<Result<PeriodComparisonDto>> Handle(GetPeriodComparisonQuery request, CancellationToken cancellationToken)
        {
            var current = new ReportingPeriod(request.From, request.To);
            var previous = current.Previous();
            var currentFrom = current.From;
            var resolved = AmountBasis.Resolve(request.Currency, request.ConvertTo, catalog);
            if (resolved.IsFailure)
                return Result.Failure<PeriodComparisonDto>(resolved.Error);
            var basis = resolved.Value;

            var rows = await context.DailyTotals
                .Within(previous.From, current.To, request.Filter, basis)
                .GroupBy(d => new { IsCurrent = d.Day >= currentFrom, d.Category })
                .Select(g => new
                {
                    g.Key.IsCurrent,
                    g.Key.Category,
                    Income = g.Sum(d => d.Income),
                    Expenses = g.Sum(d => d.Expenses),
                    ExpenseCount = g.Sum(d => d.ExpenseCount),
                    Count = g.Sum(d => d.IncomeCount + d.ExpenseCount)
                })
                .ToListAsync(cancellationToken);
            var asOf = await context.AggregationCheckpoints.AsOfAsync(cancellationToken);

            PeriodTotalsDto Totals(ReportingPeriod period, bool isCurrent)
            {
                var slice = rows.Where(r => r.IsCurrent == isCurrent).ToList();
                var income = slice.Sum(r => r.Income);
                var expenses = slice.Sum(r => r.Expenses);
                return new PeriodTotalsDto(new PeriodDto(period.From, period.To), income, expenses, income - expenses, slice.Sum(r => r.Count));
            }

            decimal Spend(bool isCurrent, TransactionCategory category) =>
                rows.Where(r => r.IsCurrent == isCurrent && r.Category == category).Sum(r => r.Expenses);

            var categories = rows
                .Where(r => r.ExpenseCount > 0)
                .Select(r => r.Category)
                .Distinct()
                .Select(category =>
                {
                    var now = Spend(true, category);
                    var before = Spend(false, category);
                    return new CategoryChangeDto(category, now, before, now - before, PercentChange(now, before));
                })
                .OrderByDescending(c => Math.Abs(c.Change))
                .ThenBy(c => c.Category)
                .ToList();

            var currentTotals = Totals(current, isCurrent: true);
            var previousTotals = Totals(previous, isCurrent: false);

            return Result.Success(new PeriodComparisonDto(
                basis.Currency,
                currentTotals,
                previousTotals,
                PercentChange(currentTotals.Expenses, previousTotals.Expenses),
                PercentChange(currentTotals.Income, previousTotals.Income),
                categories,
                asOf,
                await context.DailyTotals.DescribeConversionAsync(previous.From, current.To, request.Filter, basis, cancellationToken)));
        }

        private static decimal? PercentChange(decimal current, decimal previous) =>
            previous == 0 ? null : Math.Round((current - previous) / previous * 100, 2);
    }
}