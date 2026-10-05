using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Features.Transactions.Queries.Aggregates
{
    internal sealed class GetCashFlowQueryHandler(ITransactionsDbContext context)
        : IQueryHandler<GetCashFlowQuery, CashFlowSeriesDto>
    {
        public async Task<Result<CashFlowSeriesDto>> Handle(GetCashFlowQuery request, CancellationToken cancellationToken)
        {
            var period = new ReportingPeriod(request.From, request.To);

            var days = await context.DailyTotals
                .Within(period, request.Filter, request.Currency)
                .GroupBy(d => d.Day)
                .Select(g => new
                {
                    Day = g.Key,
                    Income = g.Sum(d => d.Income),
                    Expenses = g.Sum(d => d.Expenses),
                    Count = g.Sum(d => d.IncomeCount + d.ExpenseCount)
                })
                .ToListAsync(cancellationToken);
            var asOf = await context.AggregationCheckpoints.AsOfAsync(cancellationToken);

            var byBucket = days
                .GroupBy(d => BucketStart(d.Day, request.Granularity))
                .ToDictionary(
                    g => g.Key,
                    g => (Income: g.Sum(d => d.Income), Expenses: g.Sum(d => d.Expenses), Count: g.Sum(d => d.Count)));

            var points = new List<CashFlowPointDto>();
            for (var start = BucketStart(period.From, request.Granularity); start <= period.To; start = Next(start, request.Granularity))
            {
                var end = Next(start, request.Granularity).AddDays(-1);
                var totals = byBucket.GetValueOrDefault(start);
                points.Add(new CashFlowPointDto(
                    start < period.From ? period.From : start,
                    end > period.To ? period.To : end,
                    totals.Income,
                    totals.Expenses,
                    totals.Income - totals.Expenses,
                    totals.Count));
            }

            var income = points.Sum(p => p.Income);
            var expenses = points.Sum(p => p.Expenses);

            return Result.Success(new CashFlowSeriesDto(
                new PeriodDto(period.From, period.To),
                request.Currency,
                request.Granularity,
                income,
                expenses,
                income - expenses,
                points,
                asOf));
        }

        private static DateOnly BucketStart(DateOnly date, TimeGranularity granularity) => granularity switch
        {
            TimeGranularity.Day => date,
            TimeGranularity.Week => SouthAfricanCalendar.StartOfWeek(date),
            _ => SouthAfricanCalendar.StartOfMonth(date)
        };

        private static DateOnly Next(DateOnly bucketStart, TimeGranularity granularity) => granularity switch
        {
            TimeGranularity.Day => bucketStart.AddDays(1),
            TimeGranularity.Week => bucketStart.AddDays(7),
            _ => bucketStart.AddMonths(1)
        };
    }
}