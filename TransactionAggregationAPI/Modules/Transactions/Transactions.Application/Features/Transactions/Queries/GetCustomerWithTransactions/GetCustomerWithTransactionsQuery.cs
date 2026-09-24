using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Abstractions;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetCustomerWithTransactions
{
    public sealed record GetCustomerWithTransactionsQuery(
        Guid CustomerId,
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        TransactionCategory? Category = null,
        int Page = 1,
        int PageSize = 20) : IQuery<CustomerWithTransactionsDto>;
}