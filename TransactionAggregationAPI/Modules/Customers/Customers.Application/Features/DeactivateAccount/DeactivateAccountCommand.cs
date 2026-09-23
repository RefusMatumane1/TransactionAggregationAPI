using SharedKernel.Abstractions;

namespace Modules.Customers.Application.Features.DeactivateAccount
{
    public sealed record DeactivateAccountCommand(Guid AccountId) : ICommand;
}
