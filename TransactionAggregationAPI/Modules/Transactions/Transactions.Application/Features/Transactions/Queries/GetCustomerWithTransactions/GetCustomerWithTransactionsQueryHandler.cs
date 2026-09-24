using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Customers.Contracts;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Services;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetCustomerWithTransactions
{
    internal sealed class GetCustomerWithTransactionsQueryHandler(
        ITransactionsDbContext _context,
        ICustomersReadApi _customersReadApi,
        ILogger<GetCustomerWithTransactionsQueryHandler> logger)
        : IQueryHandler<GetCustomerWithTransactionsQuery, CustomerWithTransactionsDto>
    {
        public async Task<Result<CustomerWithTransactionsDto>> Handle(
            GetCustomerWithTransactionsQuery request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetCustomerWithTransactionsQuery for CustomerId: {CustomerId}, StartDate: {StartDate}, EndDate: {EndDate}, Category: {Category}, Page: {Page}, PageSize: {PageSize}",
                request.CustomerId, request.StartDate, request.EndDate, request.Category, request.Page, request.PageSize);

            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var customer = await _customersReadApi.FindCustomerByIdAsync(request.CustomerId, cancellationToken);

            if (customer is null)
                return Result.Failure<CustomerWithTransactionsDto>(
                    Error.NotFound("Customer", request.CustomerId));

            var transactionQuery = _context.Transactions
                .AsNoTracking()
                .Where(t => t.CustomerId == customerId)
                .AsQueryable();

            if (request.StartDate.HasValue)
            {
                var start = DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc);
                transactionQuery = transactionQuery.Where(t => t.Date >= start);
            }

            if (request.EndDate.HasValue)
            {
                var end = DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc);
                transactionQuery = transactionQuery.Where(t => t.Date <= end);
            }

            if (request.Category.HasValue)
                transactionQuery = transactionQuery.Where(t => t.Category == request.Category.Value);

            var totalTransactionCount = await transactionQuery.CountAsync(cancellationToken);

            // Stable order so pages don't overlap or skip rows between requests.
            var transactions = await transactionQuery
                .OrderByDescending(t => t.Date)
                .ThenBy(t => t.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var transactionDtos = transactions.Select(t => new TransactionDto(
                t.Id.Value,
                t.CustomerId.Value,
                t.Amount.Amount,
                t.Amount.Currency,
                t.Date,
                t.Description,
                t.Category,
                t.Status,
                t.Source.Name,
                t.AccountId != null ? t.AccountId.Value : null));

            // Totals cover the whole filtered range, not just the returned page, and use the shared
            // booked/pending rule so they match the summary and account balances.
            var booked = transactionQuery.Where(TransactionTotals.IsBooked);
            var pending = transactionQuery.Where(TransactionTotals.IsPending);

            var totalIncome = await booked.Where(t => t.Amount.Amount > 0).SumAsync(t => t.Amount.Amount, cancellationToken);
            var totalExpenses = -await booked.Where(t => t.Amount.Amount < 0).SumAsync(t => t.Amount.Amount, cancellationToken);
            var pendingIncome = await pending.Where(t => t.Amount.Amount > 0).SumAsync(t => t.Amount.Amount, cancellationToken);
            var pendingExpenses = -await pending.Where(t => t.Amount.Amount < 0).SumAsync(t => t.Amount.Amount, cancellationToken);

            var result = new CustomerWithTransactionsDto(
                customer.Id,
                customer.Email,
                customer.Name,
                customer.CreatedAt,
                customer.UpdatedAt,
                transactionDtos,
                totalTransactionCount,
                totalIncome,
                totalExpenses,
                totalIncome - totalExpenses,
                pendingIncome,
                pendingExpenses);

            return Result.Success(result);
        }
    }
}