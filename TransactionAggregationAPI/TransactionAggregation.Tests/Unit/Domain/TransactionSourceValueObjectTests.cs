using FluentAssertions;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class TransactionSourceValueObjectTests
{
    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidParameters_SetsAllProperties()
    {
        var source = TransactionSource.Create("Bank A", "EXT-123", "BankA Corp", "2.0");

        source.Name.Should().Be("Bank A");
        source.ExternalId.Should().Be("EXT-123");
        source.Provider.Should().Be("BankA Corp");
        source.Version.Should().Be("2.0");
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsDomainException()
    {
        var act = () => TransactionSource.Create("", "EXT-123");

        act.Should().Throw<DomainException>()
           .WithMessage("*source name*");
    }

    [Fact]
    public void Create_WithWhitespaceName_ThrowsDomainException()
    {
        var act = () => TransactionSource.Create("   ", "EXT-123");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithEmptyExternalId_ThrowsDomainException()
    {
        var act = () => TransactionSource.Create("Bank A", "");

        act.Should().Throw<DomainException>()
           .WithMessage("*External ID*");
    }

    [Fact]
    public void Create_WithWhitespaceExternalId_ThrowsDomainException()
    {
        var act = () => TransactionSource.Create("Bank A", "   ");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithNullOptionalParams_SetsProviderAndVersionToNull()
    {
        var source = TransactionSource.Create("Bank A", "EXT-123");

        source.Provider.Should().BeNull();
        source.Version.Should().BeNull();
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    [Fact]
    public void CreateFromBankA_SetsCorrectNameAndProvider()
    {
        var source = TransactionSource.CreateFromBankA("EXT-001");

        source.Name.Should().Be("Bank A");
        source.ExternalId.Should().Be("EXT-001");
        source.Provider.Should().Be("BankA Corp");
        source.Version.Should().Be("2.0");
    }

    [Fact]
    public void CreateFromBankB_SetsCorrectNameAndProvider()
    {
        var source = TransactionSource.CreateFromBankB("EXT-002");

        source.Name.Should().Be("Bank B");
        source.ExternalId.Should().Be("EXT-002");
        source.Provider.Should().Be("BankB Financial");
        source.Version.Should().Be("1.5");
    }

    [Fact]
    public void CreateFromWallet_SetsCorrectNameAndProvider()
    {
        var source = TransactionSource.CreateFromWallet("EXT-003");

        source.Name.Should().Be("Digital Wallet");
        source.ExternalId.Should().Be("EXT-003");
        source.Provider.Should().Be("WalletPay");
        source.Version.Should().Be("3.2");
    }

    // ── LastSyncDate ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdateLastSyncDate_SetsLastSyncDate()
    {
        var source = TransactionSource.Create("Bank A", "EXT-123");
        var syncDate = DateTime.UtcNow;

        source.UpdateLastSyncDate(syncDate);

        source.LastSyncDate.Should().Be(syncDate);
    }

    [Fact]
    public void IsOlderThan_WhenSyncDateIsOlderThanAge_ReturnsTrue()
    {
        var source = TransactionSource.Create("Bank A", "EXT-123");
        source.UpdateLastSyncDate(DateTime.UtcNow.AddHours(-2));

        source.IsOlderThan(TimeSpan.FromHours(1)).Should().BeTrue();
    }

    [Fact]
    public void IsOlderThan_WhenSyncDateIsRecent_ReturnsFalse()
    {
        var source = TransactionSource.Create("Bank A", "EXT-123");
        source.UpdateLastSyncDate(DateTime.UtcNow.AddMinutes(-5));

        source.IsOlderThan(TimeSpan.FromHours(1)).Should().BeFalse();
    }

    [Fact]
    public void IsOlderThan_WhenNoSyncDate_ReturnsFalse()
    {
        var source = TransactionSource.Create("Bank A", "EXT-123");

        source.IsOlderThan(TimeSpan.FromHours(1)).Should().BeFalse();
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void TwoSources_WithSameNameAndExternalId_AreEqual()
    {
        var a = TransactionSource.Create("Bank A", "EXT-123");
        var b = TransactionSource.Create("Bank A", "EXT-123");

        a.Should().Be(b);
    }

    [Fact]
    public void TwoSources_WithDifferentNames_AreNotEqual()
    {
        var a = TransactionSource.Create("Bank A", "EXT-123");
        var b = TransactionSource.Create("Bank B", "EXT-123");

        a.Should().NotBe(b);
    }

    [Fact]
    public void TwoSources_WithDifferentExternalIds_AreNotEqual()
    {
        var a = TransactionSource.Create("Bank A", "EXT-001");
        var b = TransactionSource.Create("Bank A", "EXT-002");

        a.Should().NotBe(b);
    }

    [Fact]
    public void TwoSources_WithSameNameAndId_DifferentProvider_AreStillEqual()
    {
        // Provider/Version are NOT equality components (only Name + ExternalId are)
        var a = TransactionSource.Create("Bank A", "EXT-123", "ProviderX");
        var b = TransactionSource.Create("Bank A", "EXT-123", "ProviderY");

        a.Should().Be(b);
    }

    // ── ToString ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_ReturnsNameAndExternalIdFormatted()
    {
        var source = TransactionSource.Create("Bank A", "EXT-123");

        source.ToString().Should().Be("Bank A (EXT-123)");
    }
}
