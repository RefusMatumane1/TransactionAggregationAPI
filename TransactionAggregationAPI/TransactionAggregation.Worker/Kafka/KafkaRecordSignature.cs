using System.Security.Cryptography;
using System.Text;

namespace TransactionAggregation.Worker.Kafka
{
    // Binding the source name stops a record signed by one provider being replayed under another.
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