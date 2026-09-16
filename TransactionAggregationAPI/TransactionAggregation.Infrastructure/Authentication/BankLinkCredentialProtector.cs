using Microsoft.AspNetCore.DataProtection;
using TransactionAggregation.Application.Common.Interfaces;

namespace TransactionAggregation.Infrastructure.Authentication
{
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