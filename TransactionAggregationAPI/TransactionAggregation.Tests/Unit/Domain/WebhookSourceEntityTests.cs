using FluentAssertions;
using TransactionAggregation.Domain.Entities;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class WebhookSourceEntityTests
{

    [Fact]
    public void Create_SetsNameAndActivatesTheSource()
    {
        var (source, _) = WebhookSource.Create("stitch");

        source.Name.Should().Be("stitch");
        source.IsActive.Should().BeTrue();
        source.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ReturnsAPlaintextKeyWhoseHashMatchesTheStoredHash()
    {
        var (source, plaintextKey) = WebhookSource.Create("stitch");

        WebhookSource.HashKey(plaintextKey).Should().Be(source.KeyHash);
    }

    [Fact]
    public void Create_TwoSources_GetDifferentKeys()
    {
        var (_, keyA) = WebhookSource.Create("source-a");
        var (_, keyB) = WebhookSource.Create("source-b");

        keyA.Should().NotBe(keyB);
    }

    [Fact]
    public void RotateKey_ReturnsANewKeyThatDoesNotMatchTheOldHash()
    {
        var (source, originalKey) = WebhookSource.Create("stitch");
        var originalHash = source.KeyHash;

        var newKey = source.RotateKey();

        newKey.Should().NotBe(originalKey);
        source.KeyHash.Should().NotBe(originalHash);
    }

    [Fact]
    public void RotateKey_NewKeysHashMatchesTheNewStoredHash()
    {
        var (source, _) = WebhookSource.Create("stitch");

        var newKey = source.RotateKey();

        WebhookSource.HashKey(newKey).Should().Be(source.KeyHash);
    }

    [Fact]
    public void RotateKey_OldKeyNoLongerHashesToTheStoredHash()
    {
        var (source, originalKey) = WebhookSource.Create("stitch");

        source.RotateKey();

        WebhookSource.HashKey(originalKey).Should().NotBe(source.KeyHash);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var (source, _) = WebhookSource.Create("stitch");

        source.Deactivate();

        source.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_AfterDeactivate_SetsIsActiveTrueAgain()
    {
        var (source, _) = WebhookSource.Create("stitch");
        source.Deactivate();

        source.Activate();

        source.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RecordUsage_SetsLastUsedAt()
    {
        var (source, _) = WebhookSource.Create("stitch");

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
}