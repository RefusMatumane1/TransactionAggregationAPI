using FluentValidation;
using Modules.WebhookSources.Application.Common;
using SharedKernel.Abstractions;

namespace Modules.WebhookSources.Application.Features.UpdateWebhookSourceInstitutions
{
    public sealed record UpdateWebhookSourceInstitutionsCommand(Guid Id, IReadOnlyList<string> AuthorizedInstitutions) : ICommand;

    public sealed class UpdateWebhookSourceInstitutionsCommandValidator : AbstractValidator<UpdateWebhookSourceInstitutionsCommand>
    {
        public UpdateWebhookSourceInstitutionsCommandValidator()
        {
            RuleFor(x => x.AuthorizedInstitutions).AuthorizedInstitutions();
        }
    }
}