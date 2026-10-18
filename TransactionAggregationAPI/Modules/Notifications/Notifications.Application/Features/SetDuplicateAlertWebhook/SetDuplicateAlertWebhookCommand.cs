using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Modules.Notifications.Domain;

namespace Modules.Notifications.Application.Features.SetDuplicateAlertWebhook
{
    public sealed record SetDuplicateAlertWebhookCommand(string BankCode, string? Url) : ICommand;

    public sealed class SetDuplicateAlertWebhookCommandValidator : AbstractValidator<SetDuplicateAlertWebhookCommand>
    {
        public SetDuplicateAlertWebhookCommandValidator()
        {
            RuleFor(x => x.BankCode).NotEmpty().MaximumLength(AlertWebhook.MaxBankCodeLength);

            RuleFor(x => x.Url)
                .Must(AlertWebhook.IsValidUrl)
                .When(x => !string.IsNullOrWhiteSpace(x.Url))
                .WithMessage($"Must be an https URL to a public host, without credentials in the address, up to {AlertWebhook.MaximumUrlLength} characters.");
        }
    }
}