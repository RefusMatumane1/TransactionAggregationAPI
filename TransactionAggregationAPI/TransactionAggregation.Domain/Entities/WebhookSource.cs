using System.Security.Cryptography;
using System.Text;
using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Domain.Entities
{
    /// <summary>
    /// One external system allowed to push transactions via the webhook (see
    /// ReceiveBankTransactionsCommandHandler/ApiKeyEndpointFilter). The key itself is never
    /// stored — only a SHA-256 hash of it, so a database compromise alone can never recover a
    /// working key. The plaintext is generated and returned exactly once, at Create/RotateKey
    /// time; there is no way to retrieve an existing key afterwards, only rotate to a new one.
    /// </summary>
    public sealed class WebhookSource : BaseEntity
    {
        private WebhookSource() { }

        public WebhookSourceId Id { get; private set; }
        public string Name { get; private set; }
        public string KeyHash { get; private set; }
        public bool IsActive { get; private set; }

        /// <summary>Set on every successful webhook authentication — lets the admin UI flag a
        /// source that's gone quiet or has never actually been used.</summary>
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

        /// <summary>Replaces this source's key with a freshly generated one and returns the new
        /// plaintext — the old key stops working immediately (its hash is overwritten, not kept
        /// around), and this new one is shown exactly once too.</summary>
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

        /// <summary>Deterministic — no salt. These are full-entropy random secrets (not human
        /// passwords), so there's no precomputed-table risk a salt would defend against, and a
        /// deterministic hash is what makes the KeyHash unique index usable for O(1) lookup by
        /// presented key (see ApiKeyEndpointFilter) instead of scanning every row.</summary>
        public static string HashKey(string rawKey) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));

        private static string GenerateApiKey() =>
            "whsk_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
