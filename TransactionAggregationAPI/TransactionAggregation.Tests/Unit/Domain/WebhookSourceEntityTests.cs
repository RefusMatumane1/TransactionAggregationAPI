using FluentAssertions;
using Modules.WebhookSources.Domain;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class WebhookSourceEntityTests
{

    [Fact]
    public void Create_SetsNameAndActivatesTheSource()
    {
        var (source, _) = WebhookSource.Create("stitch", TestInstitutions.All);

        source.Name.Should().Be("stitch");
        source.IsActive.Should().BeTrue();
        source.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ReturnsAPlaintextKeyWhoseHashMatchesTheStoredHash()
    {
        var (source, plaintextKey) = WebhookSource.Create("stitch", TestInstitutions.All);

        WebhookSource.HashKey(plaintextKey).Should().Be(source.KeyHash);
    }

    [Fact]
    public void Create_TwoSources_GetDifferentKeys()
    {
        var (_, keyA) = WebhookSource.Create("source-a", TestInstitutions.All);
        var (_, keyB) = WebhookSource.Create("source-b", TestInstitutions.All);

        keyA.Should().NotBe(keyB);
    }

    [Fact]
    public void RotateKey_ReturnsANewKeyThatDoesNotMatchTheOldHash()
    {
        var (source, originalKey) = WebhookSource.Create("stitch", TestInstitutions.All);
        var originalHash = source.KeyHash;

        var newKey = source.RotateKey();

        newKey.Should().NotBe(originalKey);
        source.KeyHash.Should().NotBe(originalHash);
    }

    [Fact]
    public void RotateKey_NewKeysHashMatchesTheNewStoredHash()
    {
        var (source, _) = WebhookSource.Create("stitch", TestInstitutions.All);

        var newKey = source.RotateKey();

        WebhookSource.HashKey(newKey).Should().Be(source.KeyHash);
    }

    [Fact]
    public void RotateKey_OldKeyNoLongerHashesToTheStoredHash()
    {
        var (source, originalKey) = WebhookSource.Create("stitch", TestInstitutions.All);

        source.RotateKey();

        WebhookSource.HashKey(originalKey).Should().NotBe(source.KeyHash);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var (source, _) = WebhookSource.Create("stitch", TestInstitutions.All);

        source.Deactivate();

        source.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_AfterDeactivate_SetsIsActiveTrueAgain()
    {
        var (source, _) = WebhookSource.Create("stitch", TestInstitutions.All);
        source.Deactivate();

        source.Activate();

        source.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RecordUsage_SetsLastUsedAt()
    {
        var (source, _) = WebhookSource.Create("stitch", TestInstitutions.All);

        source.RecordUsage();

        source.LastUsedAt.Should().NotBeNull();
        source.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
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
        var source = WebhookSource.CreateWithProvisionedKey("mock-aggregator", ProvisionedKey, TestInstitutions.All);

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
        var create = () => WebhookSource.CreateWithProvisionedKey("mock-aggregator", key, TestInstitutions.All);
        var replace = () => WebhookSource.CreateWithProvisionedKey("mock-aggregator", ProvisionedKey, TestInstitutions.All).UseProvisionedKey(key);

        create.Should().Throw<SharedKernel.Exceptions.DomainException>();
        replace.Should().Throw<SharedKernel.Exceptions.DomainException>();
    }

    [Fact]
    public void UseProvisionedKey_RetiresTheOldKeyImmediately()
    {
        var source = WebhookSource.CreateWithProvisionedKey("mock-aggregator", ProvisionedKey, TestInstitutions.All);
        const string replacement = "dev-only-replacement-key-at-least-32-characters";

        source.UseProvisionedKey(replacement);

        source.HasKey(replacement).Should().BeTrue();
        source.HasKey(ProvisionedKey).Should().BeFalse();
    }
}