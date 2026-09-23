using SharedKernel.Abstractions;
using Modules.Customers.Application.DTOs;

namespace Modules.Customers.Application.Features.GetCustomerByEmail
{
    public sealed record GetCustomerByEmailQuery(string Email) : IQuery<CustomerDto>;
}
