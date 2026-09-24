using SharedKernel.Abstractions;
using SharedKernel.Common.Attributes;

namespace Modules.Customers.Application.Features.UpdateCustomer
{
    public sealed record UpdateCustomerCommand(
        Guid CustomerId,
        [property: Sensitive] string Email,
        [property: Sensitive] string Name) : ICommand;
}