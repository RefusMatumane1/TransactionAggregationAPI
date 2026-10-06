using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace TransactionAggregation.MockAggregator.Feed
{
    // Generated on first use and kept in the data directory, so the registered public key survives restarts.
    public sealed class RecordSigningKey : IDisposable
    {
        public const string FileName = "kafka-signing-key.pem";
        public const string SignatureVersion = "tagg-kafka-record-v1";

        private readonly ECDsa _key;

        public RecordSigningKey(IOptions<MockAggregatorOptions> options, IHostEnvironment environment, ILogger<RecordSigningKey> logger)
            : this(LoadOrCreate(Path.Combine(environment.ContentRootPath, options.Value.DataDirectory, FileName), logger))
        {
        }

        public RecordSigningKey(ECDsa key) => _key = key;

        public string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

        public string Sign(string source, string? idempotencyKey, string value)
        {
            var content = Encoding.UTF8.GetBytes(string.Join('\n',
                SignatureVersion,
                source,
                idempotencyKey ?? string.Empty,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))));

            return Convert.ToBase64String(
                _key.SignData(content, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }

        public void Dispose() => _key.Dispose();

        private static ECDsa LoadOrCreate(string path, ILogger logger)
        {
            var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            if (File.Exists(path))
            {
                key.ImportFromPem(File.ReadAllText(path));
                return key;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            logger.LogWarning(
                "Generated a new Kafka signing key at {Path}. Register its public half (GET /kafka/signing-key) on the platform's webhook source, or its records will be rejected",
                path);
            return key;
        }
    }
}