using Microsoft.AspNetCore.DataProtection;
using Modules.Notifications.Application.Common;

namespace TransactionAggregation.Hosting.Security
{
    // Uses the Data Protection key ring the API and worker share through Redis, so either host can read what the other wrote.
    internal sealed class DataProtectionAlertUrlProtector(IDataProtectionProvider provider) : IAlertUrlProtector
    {
        private const string Purpose = "WebhookSources.AlertUrl.v1";

        private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

        public string Protect(string url) => _protector.Protect(url);

        public string Unprotect(string protectedUrl) => _protector.Unprotect(protectedUrl);
    }
}