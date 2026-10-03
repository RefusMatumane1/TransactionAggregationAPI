using FluentValidation;
using Modules.WebhookSources.Application.Common;

namespace Modules.WebhookSources.Application.Features.CreateWebhookSource
{
    public sealed class CreateWebhookSourceCommandValidator : AbstractValidator<CreateWebhookSourceCommand>
    {
        public CreateWebhookSourceCommandValidator()
        {
            RuleFor(x => x.Code).BankCode();
            RuleFor(x => x.DisplayName).BankDisplayName();
            RuleFor(x => x.Color).BankColor();
        }
    }
}