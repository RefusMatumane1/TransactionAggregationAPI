namespace Modules.Notifications.Application.Common
{
    // Encrypts a bank's alert URL at rest. Implemented by the host (Data Protection, whose key ring the API and worker share).
    public interface IAlertUrlProtector
    {
        string Protect(string url);

        // Throws when the value cannot be decrypted (for example the key ring is unavailable): the caller retries.
        string Unprotect(string protectedUrl);
    }
}