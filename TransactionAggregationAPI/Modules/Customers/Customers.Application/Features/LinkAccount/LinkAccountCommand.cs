using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Modules.Customers.Application.Common;
using Modules.Customers.Application.DTOs;

namespace Modules.Customers.Application.Features.LinkAccount
{
    // Links one bank account to the customer: from then on, every transaction that bank delivers for
    // that account (past and future) is part of the customer's view.
    public sealed record LinkAccountCommand(Guid CustomerId, string Institution, string ExternalAccountId) : ICommand<LinkAccountResult>;

    // Linked is false when the account was already linked (the call changed nothing).
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