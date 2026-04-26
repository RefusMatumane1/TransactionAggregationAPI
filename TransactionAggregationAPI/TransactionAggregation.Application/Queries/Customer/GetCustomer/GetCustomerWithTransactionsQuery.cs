using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Queries.Customer.GetCustomer
{
    public sealed record GetCustomerWithTransactionsQuery(
        Guid CustomerId,
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        TransactionCategory? Category = null,
        int Page = 1,
        int PageSize = 20) : IQuery<CustomerWithTransactionsDto>;
}
