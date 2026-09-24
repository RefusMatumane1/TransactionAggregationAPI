namespace Modules.BankLinks.Application.Ports
{
    public interface IBankLinkCredentialProtector
    {
        string Protect(string plaintext);
    }
}