using FluentValidation;
using Modules.WebhookSources.Application.Common;

namespace Modules.WebhookSources.Application.Features.CreateWebhookSource
{
    public sealed class CreateWebhookSourceCommandValidator : AbstractValidator<CreateWebhookSourceCommand>
    {
        public CreateWebhookSourceCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.AuthorizedInstitutions).AuthorizedInstitutions();
        }
    }
}