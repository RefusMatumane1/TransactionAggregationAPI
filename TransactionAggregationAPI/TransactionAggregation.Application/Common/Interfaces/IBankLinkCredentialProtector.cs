namespace TransactionAggregation.Application.Common.Interfaces
{
    /// <summary>
    /// Encrypts/decrypts bank-link OAuth tokens before they touch the database. Backed by
    /// ASP.NET Core Data Protection (see BankLinkCredentialProtector) so tokens are never
    /// stored in plaintext, regardless of database access.
    /// </summary>
    public interface IBankLinkCredentialProtector
    {
        string Protect(string plaintext);
        string Unprotect(string protectedValue);
    }
}
