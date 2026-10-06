using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Modules.WebhookSources.Application.Common;

namespace Modules.WebhookSources.Application.Features.UpdateWebhookSource
{
    // The code is the bank's identity on stored transactions, so only its display can change.
    public sealed record UpdateWebhookSourceCommand(Guid Id, string DisplayName, string Color) : ICommand;

    public sealed class UpdateWebhookSourceCommandValidator : AbstractValidator<UpdateWebhookSourceCommand>
    {
        public UpdateWebhookSourceCommandValidator()
        {
            RuleFor(x => x.DisplayName).BankDisplayName();
            RuleFor(x => x.Color).BankColor();
        }
    }
}