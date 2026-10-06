using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Modules.Customers.Application.Common;

namespace Modules.Customers.Application.Features.UnlinkAccount
{
    // Removes an account from the customer's view. Its transactions stay in the ledger untouched.
    public sealed record UnlinkAccountCommand(Guid CustomerId, string Institution, string ExternalAccountId) : ICommand;

    public sealed class UnlinkAccountCommandValidator : AbstractValidator<UnlinkAccountCommand>
    {
        public UnlinkAccountCommandValidator()
        {
            RuleFor(x => x.Institution).Institution().OverridePropertyName("institution");
            RuleFor(x => x.ExternalAccountId).ExternalAccountId().OverridePropertyName("externalAccountId");
        }
    }
}