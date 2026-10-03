using System.Security.Cryptography;
using System.Text;

namespace TransactionAggregation.Worker.Kafka
{
    // The bytes a provider signs (ECDSA P-256, SHA-256, IEEE P1363) and sends base64-encoded in the
    // 'signature' header. Binding the source name stops a record signed by one provider being
    // replayed under another.
    public static class KafkaRecordSignature
    {
        public const string Version = "tagg-kafka-record-v1";

        public static byte[] SignedContent(string source, string? idempotencyKey, string? value) =>
            Encoding.UTF8.GetBytes(string.Join('\n',
                Version,
                source,
                idempotencyKey ?? string.Empty,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)))));
    }
}