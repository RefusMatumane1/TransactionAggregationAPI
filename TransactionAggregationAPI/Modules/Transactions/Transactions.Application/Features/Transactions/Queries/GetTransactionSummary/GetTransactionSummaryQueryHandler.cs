using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Models;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Services;
using SharedKernel.Abstractions;
using SharedKernel.Common.Interfaces;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary
{
    internal sealed class GetTransactionSummaryQueryHandler(
        ITransactionsDbContext _context,
        ILogger<GetTransactionSummaryQueryHandler> _logger)
        : IQueryHandler<GetTransactionSummaryQuery, TransactionSummaryDto>
    {
        public async Task<Result<TransactionSummaryDto>> Handle(
            GetTransactionSummaryQuery request,
            CancellationToken cancellationToken)
        {
            var customerId = CustomerId.CreateFrom(request.CustomerId);
            var startDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc);

            var transactions = await _context.Transactions
                .Where(t =>
                    t.CustomerId == customerId &&
                    t.Date >= startDate &&
                    t.Date <= endDate)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Money figures come from booked transactions only; pending is reported alongside.
            var booked = transactions.Where(t => TransactionTotals.CountsAsBooked(t.Status)).ToList();
            var pending = transactions.Where(t => TransactionTotals.CountsAsPending(t.Status)).ToList();

            var totalIncome = booked
                .Where(t => t.Amount.Amount > 0)
                .Sum(t => t.Amount.Amount);

            var totalExpenses = booked
                .Where(t => t.Amount.Amount < 0)
                .Sum(t => Math.Abs(t.Amount.Amount));

            var spendingByCategory = booked
                .Where(t => t.Amount.Amount < 0)
                .GroupBy(t => t.Category)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(t => Math.Abs(t.Amount.Amount)));

            var monthlySummaries = booked
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g =>
                {
                    var income = g.Where(t => t.Amount.Amount > 0).Sum(t => t.Amount.Amount);
                    var expenses = g.Where(t => t.Amount.Amount < 0).Sum(t => Math.Abs(t.Amount.Amount));
                    return new MonthlySummaryDto(
                        Year: g.Key.Year,
                        Month: g.Key.Month,
                        MonthName: new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                        TotalIncome: income,
                        TotalExpenses: expenses,
                        NetBalance: income - expenses,
                        TransactionCount: g.Count());
                })
                .ToList();

            var pendingIncome = pending.Where(t => t.Amount.Amount > 0).Sum(t => t.Amount.Amount);
            var pendingExpenses = pending.Where(t => t.Amount.Amount < 0).Sum(t => Math.Abs(t.Amount.Amount));

            _logger.LogInformation(
                "Summary for customer {CustomerId}: {TransactionCount} transactions ({BookedCount} booked, {PendingCount} pending), income {Income}, expenses {Expenses}",
                request.CustomerId, transactions.Count, booked.Count, pending.Count, totalIncome, totalExpenses);

            return Result.Success(new TransactionSummaryDto(
                TotalIncome: totalIncome,
                TotalExpenses: totalExpenses,
                NetBalance: totalIncome - totalExpenses,
                SpendingByCategory: spendingByCategory,
                TotalTransactions: transactions.Count,
                MonthlySummaries: monthlySummaries,
                CompletedTransactions: booked.Count,
                PendingTransactions: pending.Count,
                PendingIncome: pendingIncome,
                PendingExpenses: pendingExpenses));
        }
    }
}