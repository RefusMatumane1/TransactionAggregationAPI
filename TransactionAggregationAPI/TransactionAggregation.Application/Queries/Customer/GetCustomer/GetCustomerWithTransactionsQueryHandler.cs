using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Queries.Customer.GetCustomer
{
    internal sealed class GetCustomerWithTransactionsQueryHandler(IApplicationDbContext _context,
        ILogger<GetCustomerWithTransactionsQueryHandler> logger)
        : IQueryHandler<GetCustomerWithTransactionsQuery, CustomerWithTransactionsDto>
    {
        public async Task<Result<CustomerWithTransactionsDto>> Handle(
            GetCustomerWithTransactionsQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Handling GetCustomerWithTransactionsQuery for CustomerId: {CustomerId}, StartDate: {StartDate}, EndDate: {EndDate}, Category: {Category}, Page: {Page}, PageSize: {PageSize}",
                    request.CustomerId, request.StartDate, request.EndDate, request.Category, request.Page, request.PageSize);

                var customerId = CustomerId.CreateFrom(request.CustomerId);

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

                if (customer is null)
                    return Result.Failure<CustomerWithTransactionsDto>(
                        Error.NotFound("Customer", request.CustomerId));

                var transactionQuery = _context.Transactions
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

                // Count all matching transactions BEFORE applying pagination
                var totalTransactionCount = await transactionQuery.CountAsync(cancellationToken);

                var transactions = await transactionQuery
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

                // Income/expenses are computed across the current page only (used for the page summary)
                var totalIncome = transactions
                    .Where(t => t.Amount.Amount > 0 && t.Status == TransactionStatus.Settled)
                    .Sum(t => t.Amount.Amount);

                var totalExpenses = transactions
                    .Where(t => t.Amount.Amount < 0 && t.Status == TransactionStatus.Settled)
                    .Sum(t => Math.Abs(t.Amount.Amount));

                var result = new CustomerWithTransactionsDto(
                    customer.Id.Value,
                    customer.Email,
                    customer.Name,
                    customer.CreatedAt,
                    customer.UpdatedAt,
                    transactionDtos,
                    totalTransactionCount,   // total across all pages, not just current page
                    totalIncome,
                    totalExpenses,
                    totalIncome - totalExpenses);

                return Result.Success(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while handling GetCustomerWithTransactionsQuery for CustomerId: {CustomerId}", request.CustomerId);
                throw;
            }
        }
    }
}
