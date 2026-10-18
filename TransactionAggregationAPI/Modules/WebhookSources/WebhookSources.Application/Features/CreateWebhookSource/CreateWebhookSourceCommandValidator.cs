using FluentValidation;
using Modules.Notifications.Contracts;
using Modules.WebhookSources.Application.Common;

namespace Modules.WebhookSources.Application.Features.CreateWebhookSource
{
    public sealed class CreateWebhookSourceCommandValidator : AbstractValidator<CreateWebhookSourceCommand>
    {
        public CreateWebhookSourceCommandValidator(IDuplicateAlertWebhooks notifications)
        {
            RuleFor(x => x.Code).BankCode();
            RuleFor(x => x.DisplayName).BankDisplayName();
            RuleFor(x => x.Color).BankColor();

            RuleFor(x => x.NotificationUrl)
                .Must(notifications.IsValidUrl)
                .When(x => !string.IsNullOrWhiteSpace(x.NotificationUrl))
                .WithMessage("Must be an https URL to a public host, without credentials in the address.");
        }
    }
}