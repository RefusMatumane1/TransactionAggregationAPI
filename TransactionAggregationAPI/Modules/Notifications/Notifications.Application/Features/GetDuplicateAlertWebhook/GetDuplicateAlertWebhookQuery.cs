using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Modules.Notifications.Application.DTOs;
using Modules.Notifications.Domain;

namespace Modules.Notifications.Application.Features.GetDuplicateAlertWebhook
{
    public sealed record GetDuplicateAlertWebhookQuery(string BankCode) : IQuery<DuplicateAlertWebhookDto>;

    public sealed class GetDuplicateAlertWebhookQueryValidator : AbstractValidator<GetDuplicateAlertWebhookQuery>
    {
        public GetDuplicateAlertWebhookQueryValidator() =>
            RuleFor(x => x.BankCode).NotEmpty().MaximumLength(AlertWebhook.MaxBankCodeLength);
    }
}