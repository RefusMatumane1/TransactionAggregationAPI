using Modules.Customers.Application.DTOs;
using SharedKernel.Abstractions;
using SharedKernel.Common.Attributes;

namespace Modules.Customers.Application.Features.GetCustomerByEmail
{
    /// <summary>
    /// Scoped to <paramref name="CustomerId"/>: ownership is part of the lookup itself, so
    /// another customer's email address and a missing one run the same query, match no row and
    /// fail at the same point — neither the response nor its timing tells them apart.
    /// </summary>
    public sealed record GetCustomerByEmailQuery([property: Sensitive] string Email, Guid CustomerId) : IQuery<CustomerDto>;
}