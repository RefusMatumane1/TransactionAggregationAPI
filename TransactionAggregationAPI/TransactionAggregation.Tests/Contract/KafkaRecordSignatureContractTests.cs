using FluentAssertions;
using Modules.WebhookSources.Domain;
using System.Security.Cryptography;
using TransactionAggregation.MockAggregator.Feed;
using TransactionAggregation.Worker.Kafka;
using Xunit;

namespace TransactionAggregation.Tests.Contract
{
    // The sender (the mock banks) and the platform each build the signed bytes independently,
    // as a real bank would from the published record format. They must agree byte for byte.
    public class KafkaRecordSignatureContractTests
    {
        private const string Source = "mock-aggregator";
        private const string Value = """{"externalAccountId":"acc-1","transactions":[{"id":"t-1","amount":-12.5}]}""";

        [Theory]
        [InlineData("delivery-1")]
        [InlineData(null)]
        public void ARecordSignedByTheProvider_VerifiesOnThePlatform(string? idempotencyKey)
        {
            using var providerKey = new RecordSigningKey(ECDsa.Create(ECCurve.NamedCurves.nistP256));
            var source = WebhookSource.Create(Source, Source, "#123456").Source;
            source.RegisterSigningPublicKey(providerKey.PublicKey);

            var signature = providerKey.Sign(Source, idempotencyKey, Value);

            source.HasValidSignature(
                    KafkaRecordSignature.SignedContent(Source, idempotencyKey, Value),
                    Convert.FromBase64String(signature))
                .Should().BeTrue();
        }

        [Fact]
        public void BothSides_UseTheSameFormatVersion() =>
            RecordSigningKey.SignatureVersion.Should().Be(KafkaRecordSignature.Version);
    }
}