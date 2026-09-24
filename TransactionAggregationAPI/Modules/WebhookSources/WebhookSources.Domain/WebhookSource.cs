using Modules.WebhookSources.Domain.ValueObjects;
using SharedKernel.Common;
using SharedKernel.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace Modules.WebhookSources.Domain
{
    public sealed class WebhookSource : BaseEntity
    {
        private WebhookSource() { }

        public WebhookSourceId Id { get; private set; } = null!;
        public string Name { get; private set; } = null!;
        public string KeyHash { get; private set; } = null!;
        public bool IsActive { get; private set; }

        public DateTime? LastUsedAt { get; private set; }

        /// <summary>
        /// The institutions this source may deliver transactions for. A delivery is only ever
        /// applied to bank links at one of these institutions, so a leaked or misbehaving key
        /// can't write into accounts held at a bank the source doesn't serve. Empty means
        /// authorized for nothing — the safe default for a source nobody has scoped yet.
        /// </summary>
        public List<string> AuthorizedInstitutions { get; private set; } = [];

        public const int MaximumInstitutionNameLength = 50;

        public static (WebhookSource Source, string PlaintextKey) Create(string name, IEnumerable<string> authorizedInstitutions)
        {
            var plaintextKey = GenerateApiKey();

            var source = new WebhookSource
            {
                Id = WebhookSourceId.Create(),
                Name = name,
                KeyHash = HashKey(plaintextKey),
                IsActive = true,
                AuthorizedInstitutions = NormalizeInstitutions(authorizedInstitutions)
            };

            return (source, plaintextKey);
        }

        /// <summary>
        /// Minimum length for a key issued outside this system — the generated keys are
        /// 32 random bytes (~43 characters), so anything shorter would be the weak link.
        /// </summary>
        public const int MinimumProvisionedKeyLength = 32;

        /// <summary>
        /// Registers a source whose key was issued out-of-band (e.g. shared with a sender's
        /// deployment configuration) instead of generated here. Only the hash is kept.
        /// </summary>
        public static WebhookSource CreateWithProvisionedKey(
            string name, string plaintextKey, IEnumerable<string> authorizedInstitutions)
        {
            EnsureStrongEnough(plaintextKey);

            return new WebhookSource
            {
                Id = WebhookSourceId.Create(),
                Name = name,
                KeyHash = HashKey(plaintextKey),
                IsActive = true,
                AuthorizedInstitutions = NormalizeInstitutions(authorizedInstitutions)
            };
        }

        /// <summary>Replaces the set of institutions this source may deliver for.</summary>
        public void AuthorizeInstitutions(IEnumerable<string> institutions)
        {
            AuthorizedInstitutions = NormalizeInstitutions(institutions);
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsAuthorizedFor(string institution) =>
            AuthorizedInstitutions.Contains(institution.Trim(), StringComparer.OrdinalIgnoreCase);

        private static List<string> NormalizeInstitutions(IEnumerable<string> institutions)
        {
            var normalized = institutions
                .Select(i => i?.Trim() ?? string.Empty)
                .ToList();

            if (normalized.Count == 0 || normalized.Any(i => i.Length == 0 || i.Length > MaximumInstitutionNameLength))
                throw new DomainException(
                    $"A webhook source must be authorized for at least one institution, each named in 1-{MaximumInstitutionNameLength} characters.");

            return normalized.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        }

        /// <summary>Replaces the key with one issued out-of-band; the old key stops working immediately.</summary>
        public void UseProvisionedKey(string plaintextKey)
        {
            EnsureStrongEnough(plaintextKey);
            KeyHash = HashKey(plaintextKey);
            UpdatedAt = DateTime.UtcNow;
        }

        public bool HasKey(string plaintextKey) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(KeyHash), Encoding.UTF8.GetBytes(HashKey(plaintextKey)));

        private static void EnsureStrongEnough(string plaintextKey)
        {
            if (string.IsNullOrWhiteSpace(plaintextKey) || plaintextKey.Length < MinimumProvisionedKeyLength)
                throw new DomainException(
                    $"A provisioned webhook key must be at least {MinimumProvisionedKeyLength} characters.");
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