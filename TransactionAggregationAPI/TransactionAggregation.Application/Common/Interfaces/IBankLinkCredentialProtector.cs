namespace TransactionAggregation.Application.Common.Interfaces
{
    public interface IBankLinkCredentialProtector
    {
        string Protect(string plaintext);
        string Unprotect(string protectedValue);
    }
}