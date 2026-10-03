using FluentAssertions;
using Modules.WebhookSources.Domain;
using SharedKernel.Exceptions;
using System.Security.Cryptography;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain
{
    public class WebhookSourceSigningKeyTests
    {
        private static readonly byte[] Content = "tagg-kafka-record-v1\nstitch\n\nabc"u8.ToArray();

        private static WebhookSource Source() => WebhookSource.Create("stitch", "stitch", "#123456").Source;

        private static string PublicKeyOf(AsymmetricAlgorithm key) => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

        private static byte[] Sign(ECDsa key, byte[] content) =>
            key.SignData(content, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        [Fact]
        public void RegisteredP256Key_VerifiesItsOwnSignature_AndRejectsAnyOther()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var source = Source();

            source.RegisterSigningPublicKey(PublicKeyOf(key));

            source.HasValidSignature(Content, Sign(key, Content)).Should().BeTrue();
            source.HasValidSignature(Content, Sign(otherKey, Content)).Should().BeFalse();
            source.HasValidSignature([.. Content, 0], Sign(key, Content)).Should().BeFalse();
        }

        [Fact]
        public void NoRegisteredKey_NothingVerifies()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            Source().HasValidSignature(Content, Sign(key, Content)).Should().BeFalse();
        }

        public static TheoryData<string, string> UnacceptableKeys()
        {
            using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            using var rsa = RSA.Create(2048);
            return new()
            {
                { "P-384 key", PublicKeyOf(p384) },
                { "RSA key", PublicKeyOf(rsa) },
                { "not base64", "not a key!" },
                { "base64 of garbage", Convert.ToBase64String(new byte[64]) },
                { "empty", "" },
                { "longer than the column", new string('A', WebhookSource.MaximumSigningPublicKeyLength + 4) }
            };
        }

        [Theory]
        [MemberData(nameof(UnacceptableKeys))]
        public void OnlyAnEcdsaP256PublicKey_CanBeRegistered(string scenario, string publicKey)
        {
            WebhookSource.IsValidSigningPublicKey(publicKey).Should().BeFalse(scenario);

            var register = () => Source().RegisterSigningPublicKey(publicKey);
            register.Should().Throw<DomainException>(scenario);
        }

        [Fact]
        public void RegisteredKey_IsStoredInCanonicalForm()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var source = Source();

            source.RegisterSigningPublicKey($"  {PublicKeyOf(key)}\n");

            source.SigningPublicKey.Should().Be(PublicKeyOf(key));
        }
    }
}