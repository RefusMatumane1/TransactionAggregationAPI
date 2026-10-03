using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Features.Transactions.Queries.Aggregates
{
    internal sealed class GetInstitutionBreakdownQueryHandler(ITransactionsDbContext context)
        : IQueryHandler<GetInstitutionBreakdownQuery, InstitutionBreakdownDto>
    {
        public async Task<Result<InstitutionBreakdownDto>> Handle(GetInstitutionBreakdownQuery request, CancellationToken cancellationToken)
        {
            var period = new ReportingPeriod(request.From, request.To);

            var rows = await context.DailyTotals
                .Within(period, request.Filter, request.Currency)
                .GroupBy(d => new { Institution = d.SourceName, d.ExternalAccountId })
                .Select(g => new
                {
                    g.Key.Institution,
                    g.Key.ExternalAccountId,
                    Income = g.Sum(d => d.Income),
                    Expenses = g.Sum(d => d.Expenses),
                    Count = g.Sum(d => d.IncomeCount + d.ExpenseCount)
                })
                .ToListAsync(cancellationToken);
            var asOf = await context.AggregationCheckpoints.AsOfAsync(cancellationToken);

            var institutions = rows
                .GroupBy(r => r.Institution)
                .Select(g =>
                {
                    var accounts = g
                        .Select(a => new AccountTotalDto(a.ExternalAccountId, a.Income, a.Expenses, a.Income - a.Expenses, a.Count))
                        .OrderByDescending(a => a.Expenses)
                        .ToList();
                    var income = accounts.Sum(a => a.Income);
                    var expenses = accounts.Sum(a => a.Expenses);
                    return new InstitutionTotalDto(g.Key, income, expenses, income - expenses, accounts.Sum(a => a.TransactionCount), accounts.Count, accounts);
                })
                .OrderByDescending(i => i.Expenses)
                .ThenBy(i => i.Institution, StringComparer.Ordinal)
                .ToList();

            return Result.Success(new InstitutionBreakdownDto(
                new PeriodDto(period.From, period.To), request.Currency, institutions, asOf));
        }
    }
}