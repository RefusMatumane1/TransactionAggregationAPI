using SharedKernel.Abstractions;
using SharedKernel.Common.Attributes;

namespace Modules.Customers.Application.Features.CreateCustomer
{
    public sealed record CreateCustomerCommand(
        [property: Sensitive] string Email,
        [property: Sensitive] string Name,
        [property: Sensitive] string Password) : ICommand<Guid>;
}