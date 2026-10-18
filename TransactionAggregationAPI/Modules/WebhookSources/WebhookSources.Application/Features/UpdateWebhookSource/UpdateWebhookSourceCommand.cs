using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Modules.WebhookSources.Application.Common;

namespace Modules.WebhookSources.Application.Features.UpdateWebhookSource
{
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