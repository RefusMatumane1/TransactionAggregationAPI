using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Features.Transactions.Queries.Aggregates
{
    internal sealed class GetPeriodComparisonQueryHandler(ITransactionsDbContext context)
        : IQueryHandler<GetPeriodComparisonQuery, PeriodComparisonDto>
    {
        public async Task<Result<PeriodComparisonDto>> Handle(GetPeriodComparisonQuery request, CancellationToken cancellationToken)
        {
            var current = new ReportingPeriod(request.From, request.To);
            var previous = current.Previous();
            var currentFrom = current.From;

            var rows = await context.DailyTotals
                .Within(previous.From, current.To, request.Filter, request.Currency)
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

            decimal Spend(bool isCurrent, Domain.Enums.TransactionCategory category) =>
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
                request.Currency,
                currentTotals,
                previousTotals,
                PercentChange(currentTotals.Expenses, previousTotals.Expenses),
                PercentChange(currentTotals.Income, previousTotals.Income),
                categories,
                asOf));
        }

        private static decimal? PercentChange(decimal current, decimal previous) =>
            previous == 0 ? null : Math.Round((current - previous) / previous * 100, 2);
    }
}