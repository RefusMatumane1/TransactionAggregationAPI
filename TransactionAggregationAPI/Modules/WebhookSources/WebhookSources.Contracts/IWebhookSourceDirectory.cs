namespace Modules.WebhookSources.Contracts
{
    public enum SignatureVerification
    {
        Invalid,
        UnknownOrInactiveSource,
        NoSigningKeyRegistered,
        Valid
    }

    public interface IWebhookSourceDirectory
    {
        Task<SignatureVerification> VerifySignatureAsync(
            string sourceName, byte[] signedContent, byte[] signature, CancellationToken cancellationToken = default);
    }
}