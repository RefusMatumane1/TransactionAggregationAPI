using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Modules.WebhookSources.Domain;

namespace Modules.WebhookSources.Application.Features.RegisterWebhookSourceSigningKey
{
    public sealed record RegisterWebhookSourceSigningKeyCommand(Guid Id, string PublicKey) : ICommand;

    public sealed class RegisterWebhookSourceSigningKeyCommandValidator : AbstractValidator<RegisterWebhookSourceSigningKeyCommand>
    {
        public RegisterWebhookSourceSigningKeyCommandValidator()
        {
            RuleFor(x => x.PublicKey)
                .Must(WebhookSource.IsValidSigningPublicKey)
                .WithMessage("Must be a base64 SubjectPublicKeyInfo for an ECDSA P-256 public key.");
        }
    }
}