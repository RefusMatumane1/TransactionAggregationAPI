using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Queries.Customer.GetCustomer
{
    public sealed record GetCustomerByEmailQuery(string Email) : IQuery<CustomerDto>;
}
