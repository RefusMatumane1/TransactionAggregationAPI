using FluentValidation;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.CreateWebhookSource
{
    public sealed class CreateWebhookSourceCommandValidator : AbstractValidator<CreateWebhookSourceCommand>
    {
        public CreateWebhookSourceCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        }
    }
}