namespace Modules.Notifications.Application.Common
{
    public interface IAlertUrlProtector
    {
        string Protect(string url);

        string Unprotect(string protectedUrl);
    }
}