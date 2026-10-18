using SharedKernel.Exceptions;
using System.Net;

namespace Modules.Notifications.Domain
{
    public sealed class AlertWebhook
    {
        public const int MaxBankCodeLength = 50;
        public const int MaximumUrlLength = 2000;
        public const int MaximumProtectedUrlLength = 4000;

        private AlertWebhook() { }

        public string BankCode { get; private set; } = null!;
        public string ProtectedUrl { get; private set; } = null!;
        public DateTime UpdatedAt { get; private set; }

        public static AlertWebhook Create(string bankCode, string protectedUrl)
        {
            if (string.IsNullOrWhiteSpace(bankCode) || bankCode.Length > MaxBankCodeLength)
                throw new DomainException($"A bank code must be 1-{MaxBankCodeLength} characters.");

            var webhook = new AlertWebhook { BankCode = bankCode };
            webhook.Replace(protectedUrl);
            return webhook;
        }

        public void Replace(string protectedUrl)
        {
            if (string.IsNullOrWhiteSpace(protectedUrl) || protectedUrl.Length > MaximumProtectedUrlLength)
                throw new DomainException("The protected alert webhook URL is empty or too long.");

            ProtectedUrl = protectedUrl;
            UpdatedAt = DateTime.UtcNow;
        }

        public static bool IsValidUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Length > MaximumUrlLength || url.Any(char.IsControl))
                return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(uri.UserInfo))
                return false;

            var host = uri.IdnHost;
            if (IPAddress.TryParse(host.Trim('[', ']'), out var address))
                return IsPublic(address);

            return host.Contains('.')
                && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                && !host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
                && !host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
                && !host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPublic(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
                || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                return false;

            var bytes = address.GetAddressBytes();
            if (bytes.Length == 16)
                return (bytes[0] & 0xFE) != 0xFC;

            return !(bytes[0] == 10
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168
                || bytes[0] == 169 && bytes[1] == 254
                || bytes[0] == 100 && bytes[1] is >= 64 and <= 127
                || bytes[0] == 0
                || bytes[0] >= 224);
        }
    }
}