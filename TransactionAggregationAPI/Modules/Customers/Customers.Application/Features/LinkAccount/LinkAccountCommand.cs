using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Modules.Customers.Application.Common;
using Modules.Customers.Application.DTOs;

namespace Modules.Customers.Application.Features.LinkAccount
{
    public sealed record LinkAccountCommand(Guid CustomerId, string Institution, string ExternalAccountId) : ICommand<LinkAccountResult>;

    public sealed record LinkAccountResult(CustomerDto Customer, bool Linked);

    public sealed class LinkAccountCommandValidator : AbstractValidator<LinkAccountCommand>
    {
        public LinkAccountCommandValidator()
        {
            RuleFor(x => x.Institution).Institution().OverridePropertyName("institution");
            RuleFor(x => x.ExternalAccountId).ExternalAccountId().OverridePropertyName("externalAccountId");
        }
    }
}