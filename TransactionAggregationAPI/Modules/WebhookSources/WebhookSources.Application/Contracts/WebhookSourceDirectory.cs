using Microsoft.EntityFrameworkCore;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Contracts;

namespace Modules.WebhookSources.Application.Contracts
{
    internal sealed class WebhookSourceDirectory(IWebhookSourcesDbContext context) : IWebhookSourceDirectory
    {
        public async Task<SignatureVerification> VerifySignatureAsync(
            string sourceName, byte[] signedContent, byte[] signature, CancellationToken cancellationToken = default)
        {
            var source = await context.WebhookSources
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Name == sourceName && s.IsActive, cancellationToken);

            if (source is null)
                return SignatureVerification.UnknownOrInactiveSource;
            if (source.SigningPublicKey is null)
                return SignatureVerification.NoSigningKeyRegistered;

            return source.HasValidSignature(signedContent, signature)
                ? SignatureVerification.Valid
                : SignatureVerification.Invalid;
        }

        public Task<string?> FindBankCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var lowered = code.Trim().ToLowerInvariant();
            return context.WebhookSources
                .AsNoTracking()
                .Where(s => s.Name.ToLower() == lowered)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}