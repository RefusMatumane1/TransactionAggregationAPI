using Microsoft.AspNetCore.DataProtection;
using TransactionAggregation.Application.Common.Interfaces;

namespace TransactionAggregation.Infrastructure.Authentication
{
    /// <summary>
    /// Encrypts bank-link OAuth tokens with ASP.NET Core Data Protection before they're
    /// persisted. The purpose string ("BankLink.Tokens.v1") scopes the key so it can never be
    /// used to decrypt anything else Data Protection might later be used for in this app; bump
    /// the version suffix (and treat existing ciphertext as unrecoverable) if this ever needs
    /// to be re-keyed.
    /// </summary>
    internal sealed class BankLinkCredentialProtector : IBankLinkCredentialProtector
    {
        private readonly IDataProtector _protector;

        public BankLinkCredentialProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("BankLink.Tokens.v1");
        }

        public string Protect(string plaintext) => _protector.Protect(plaintext);

        public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
    }
}
