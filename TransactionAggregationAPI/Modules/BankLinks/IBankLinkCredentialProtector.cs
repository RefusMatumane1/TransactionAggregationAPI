namespace Modules.BankLinks
{
    public interface IBankLinkCredentialProtector
    {
        string Protect(string plaintext);
        string Unprotect(string protectedValue);
    }
}
