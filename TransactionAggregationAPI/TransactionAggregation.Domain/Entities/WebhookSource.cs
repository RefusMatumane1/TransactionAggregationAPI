using System.Security.Cryptography;
using System.Text;
using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Domain.Entities
{
    public sealed class WebhookSource : BaseEntity
    {
        private WebhookSource() { }

        public WebhookSourceId Id { get; private set; } = null!;
        public string Name { get; private set; } = null!;
        public string KeyHash { get; private set; } = null!;
        public bool IsActive { get; private set; }

        public DateTime? LastUsedAt { get; private set; }

        public static (WebhookSource Source, string PlaintextKey) Create(string name)
        {
            var plaintextKey = GenerateApiKey();

            var source = new WebhookSource
            {
                Id = WebhookSourceId.Create(),
                Name = name,
                KeyHash = HashKey(plaintextKey),
                IsActive = true
            };

            return (source, plaintextKey);
        }

        public string RotateKey()
        {
            var plaintextKey = GenerateApiKey();
            KeyHash = HashKey(plaintextKey);
            UpdatedAt = DateTime.UtcNow;
            return plaintextKey;
        }

        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RecordUsage()
        {
            LastUsedAt = DateTime.UtcNow;
        }

        public static string HashKey(string rawKey) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));

        private static string GenerateApiKey() =>
            "whsk_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}