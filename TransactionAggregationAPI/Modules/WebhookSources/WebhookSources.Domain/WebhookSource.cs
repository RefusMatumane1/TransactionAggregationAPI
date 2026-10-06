using Modules.WebhookSources.Domain.ValueObjects;
using SharedKernel.Common;
using SharedKernel.Exceptions;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Modules.WebhookSources.Domain
{
    public sealed class WebhookSource : BaseEntity
    {
        private WebhookSource() { }

        public WebhookSourceId Id { get; private set; } = null!;

        // The bank's identity everywhere: inbox/audit SourceName, Kafka `source` header, transaction institution. Immutable.
        public string Name { get; private set; } = null!;

        public string DisplayName { get; private set; } = null!;

        public string Color { get; private set; } = null!;
        public string KeyHash { get; private set; } = null!;
        public bool IsActive { get; private set; }

        public DateTime? LastUsedAt { get; private set; }

        public string? SigningPublicKey { get; private set; }

        public const int MaximumCodeLength = 50;
        public const int MaximumDisplayNameLength = 100;
        public const int MaximumSigningPublicKeyLength = 200;
        public const string DefaultColor = "#6C757D";

        private static readonly Regex CodePattern = new("^[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.Compiled);
        private static readonly Regex ColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        private const string P256CurveOid = "1.2.840.10045.3.1.7";

        public static bool IsValidCode(string? code) =>
            !string.IsNullOrEmpty(code) && code.Length <= MaximumCodeLength && CodePattern.IsMatch(code);

        public static bool IsValidColor(string? color) => color is not null && ColorPattern.IsMatch(color);

        public static (WebhookSource Source, string PlaintextKey) Create(string code, string displayName, string color)
        {
            var plaintextKey = GenerateApiKey();
            var source = New(code, displayName, color);
            source.KeyHash = HashKey(plaintextKey);
            return (source, plaintextKey);
        }

        public const int MinimumProvisionedKeyLength = 32;

        public static WebhookSource CreateWithProvisionedKey(string code, string plaintextKey, string displayName, string color)
        {
            EnsureStrongEnough(plaintextKey);
            var source = New(code, displayName, color);
            source.KeyHash = HashKey(plaintextKey);
            return source;
        }

        private static WebhookSource New(string code, string displayName, string color)
        {
            if (!IsValidCode(code))
                throw new DomainException(
                    $"A bank code must be 1-{MaximumCodeLength} letters, digits, '-' or '_', starting with a letter or digit.");

            var source = new WebhookSource { Id = WebhookSourceId.Create(), Name = code, IsActive = true };
            source.Describe(displayName, color);
            return source;
        }

        public void UpdateDetails(string displayName, string color)
        {
            Describe(displayName, color);
            UpdatedAt = DateTime.UtcNow;
        }

        private void Describe(string displayName, string color)
        {
            var name = displayName?.Trim() ?? string.Empty;
            if (name.Length == 0 || name.Length > MaximumDisplayNameLength)
                throw new DomainException($"A display name must be 1-{MaximumDisplayNameLength} characters.");
            if (!IsValidColor(color))
                throw new DomainException("A colour must be a hex value like #0033A1.");

            DisplayName = name;
            Color = color.ToUpperInvariant();
        }

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

        public static readonly TimeSpan UsageRecordingInterval = TimeSpan.FromMinutes(1);

        // Throttled: writing it on every delivery would make the row a write hot spot.
        public bool RecordUsage(DateTime now)
        {
            if (LastUsedAt is { } last && now - last < UsageRecordingInterval)
                return false;

            LastUsedAt = now;
            return true;
        }

        public void RegisterSigningPublicKey(string subjectPublicKeyInfoBase64)
        {
            SigningPublicKey = NormalizeSigningPublicKey(subjectPublicKeyInfoBase64)
                ?? throw new DomainException("The signing key must be a base64 SubjectPublicKeyInfo for an ECDSA P-256 public key.");
            UpdatedAt = DateTime.UtcNow;
        }

        public static bool IsValidSigningPublicKey(string? subjectPublicKeyInfoBase64) =>
            NormalizeSigningPublicKey(subjectPublicKeyInfoBase64) is not null;

        public bool HasValidSignature(byte[] signedContent, byte[] signature)
        {
            if (SigningPublicKey is null)
                return false;

            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(SigningPublicKey), out _);
            return key.VerifyData(signedContent, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }

        private static string? NormalizeSigningPublicKey(string? subjectPublicKeyInfoBase64)
        {
            if (string.IsNullOrWhiteSpace(subjectPublicKeyInfoBase64) || subjectPublicKeyInfoBase64.Length > MaximumSigningPublicKeyLength)
                return null;

            try
            {
                using var key = ECDsa.Create();
                key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(subjectPublicKeyInfoBase64.Trim()), out _);
                return IsP256(key.ExportParameters(includePrivateParameters: false).Curve)
                    ? Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())
                    : null;
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                return null;
            }
        }

        private static bool IsP256(ECCurve curve) =>
            curve.IsNamed
            && (curve.Oid.Value == P256CurveOid || curve.Oid.FriendlyName is "nistP256" or "ECDSA_P256" or "secp256r1");

        public static string HashKey(string rawKey) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));

        private static string GenerateApiKey() =>
            "whsk_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}