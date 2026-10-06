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

        // The code as registered ("FNB" for "fnb"), or null if no such bank exists.
        Task<string?> FindBankCodeAsync(string code, CancellationToken cancellationToken = default);
    }
}