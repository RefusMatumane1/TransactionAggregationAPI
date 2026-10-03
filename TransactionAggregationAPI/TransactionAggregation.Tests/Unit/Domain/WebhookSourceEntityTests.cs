using FluentAssertions;
using Modules.WebhookSources.Domain;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain
{
    public class WebhookSourceEntityTests
    {
        [Fact]
        public void Create_SetsNameAndActivatesTheSource()
        {
            var (source, _) = WebhookSource.Create("stitch", "stitch", "#123456");

            source.Name.Should().Be("stitch");
            source.IsActive.Should().BeTrue();
            source.LastUsedAt.Should().BeNull();
        }

        [Fact]
        public void Create_ReturnsAPlaintextKeyWhoseHashMatchesTheStoredHash()
        {
            var (source, plaintextKey) = WebhookSource.Create("stitch", "stitch", "#123456");

            WebhookSource.HashKey(plaintextKey).Should().Be(source.KeyHash);
        }

        [Fact]
        public void Create_TwoSources_GetDifferentKeys()
        {
            var (_, keyA) = WebhookSource.Create("source-a", "source-a", "#123456");
            var (_, keyB) = WebhookSource.Create("source-b", "source-b", "#123456");

            keyA.Should().NotBe(keyB);
        }

        [Fact]
        public void RotateKey_ReturnsANewKeyThatDoesNotMatchTheOldHash()
        {
            var (source, originalKey) = WebhookSource.Create("stitch", "stitch", "#123456");
            var originalHash = source.KeyHash;

            var newKey = source.RotateKey();

            newKey.Should().NotBe(originalKey);
            source.KeyHash.Should().NotBe(originalHash);
        }

        [Fact]
        public void RotateKey_NewKeysHashMatchesTheNewStoredHash()
        {
            var (source, _) = WebhookSource.Create("stitch", "stitch", "#123456");

            var newKey = source.RotateKey();

            WebhookSource.HashKey(newKey).Should().Be(source.KeyHash);
        }

        [Fact]
        public void RotateKey_OldKeyNoLongerHashesToTheStoredHash()
        {
            var (source, originalKey) = WebhookSource.Create("stitch", "stitch", "#123456");

            source.RotateKey();

            WebhookSource.HashKey(originalKey).Should().NotBe(source.KeyHash);
        }

        [Fact]
        public void Deactivate_SetsIsActiveFalse()
        {
            var (source, _) = WebhookSource.Create("stitch", "stitch", "#123456");

            source.Deactivate();

            source.IsActive.Should().BeFalse();
        }

        [Fact]
        public void Activate_AfterDeactivate_SetsIsActiveTrueAgain()
        {
            var (source, _) = WebhookSource.Create("stitch", "stitch", "#123456");
            source.Deactivate();

            source.Activate();

            source.IsActive.Should().BeTrue();
        }

        [Fact]
        public void RecordUsage_FirstUse_SetsLastUsedAt()
        {
            var (source, _) = WebhookSource.Create("stitch", "stitch", "#123456");
            var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

            source.RecordUsage(now).Should().BeTrue();

            source.LastUsedAt.Should().Be(now);
        }

        [Fact]
        public void RecordUsage_WithinTheInterval_ChangesNothing_SoNothingIsWritten()
        {
            var (source, _) = WebhookSource.Create("stitch", "stitch", "#123456");
            var first = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
            source.RecordUsage(first);

            source.RecordUsage(first + WebhookSource.UsageRecordingInterval - TimeSpan.FromSeconds(1)).Should().BeFalse();

            source.LastUsedAt.Should().Be(first);
        }

        [Fact]
        public void RecordUsage_AfterTheInterval_RecordsTheNewTime()
        {
            var (source, _) = WebhookSource.Create("stitch", "stitch", "#123456");
            var first = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
            source.RecordUsage(first);
            var later = first + WebhookSource.UsageRecordingInterval;

            source.RecordUsage(later).Should().BeTrue();

            source.LastUsedAt.Should().Be(later);
        }

        [Fact]
        public void HashKey_IsDeterministic_SameInputSameOutput()
        {
            WebhookSource.HashKey("whsk_example").Should().Be(WebhookSource.HashKey("whsk_example"));
        }

        [Fact]
        public void HashKey_DifferentInputs_ProduceDifferentHashes()
        {
            WebhookSource.HashKey("whsk_one").Should().NotBe(WebhookSource.HashKey("whsk_two"));
        }

        [Fact]
        public void HashKey_ProducesA64CharacterHexString()
        {
            var hash = WebhookSource.HashKey("whsk_example");

            hash.Should().HaveLength(64);
            hash.Should().MatchRegex("^[0-9A-F]+$");
        }

        private const string ProvisionedKey = "dev-only-provisioned-key-at-least-32-characters";

        [Fact]
        public void CreateWithProvisionedKey_StoresOnlyTheHash()
        {
            var source = WebhookSource.CreateWithProvisionedKey("mock-aggregator", ProvisionedKey, "mock-aggregator", "#123456");

            source.IsActive.Should().BeTrue();
            source.KeyHash.Should().Be(WebhookSource.HashKey(ProvisionedKey));
            source.KeyHash.Should().NotContain(ProvisionedKey);
            source.HasKey(ProvisionedKey).Should().BeTrue();
            source.HasKey("some-other-key-that-is-also-long-enough").Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("too-short-for-a-webhook-key")]
        public void ProvisionedKey_ShorterThanAGeneratedOne_IsRefused(string key)
        {
            var create = () => WebhookSource.CreateWithProvisionedKey("mock-aggregator", key, "mock-aggregator", "#123456");
            var replace = () => WebhookSource.CreateWithProvisionedKey("mock-aggregator", ProvisionedKey, "mock-aggregator", "#123456").UseProvisionedKey(key);

            create.Should().Throw<SharedKernel.Exceptions.DomainException>();
            replace.Should().Throw<SharedKernel.Exceptions.DomainException>();
        }

        [Fact]
        public void UseProvisionedKey_RetiresTheOldKeyImmediately()
        {
            var source = WebhookSource.CreateWithProvisionedKey("mock-aggregator", ProvisionedKey, "mock-aggregator", "#123456");
            const string replacement = "dev-only-replacement-key-at-least-32-characters";

            source.UseProvisionedKey(replacement);

            source.HasKey(replacement).Should().BeTrue();
            source.HasKey(ProvisionedKey).Should().BeFalse();
        }
    }
}