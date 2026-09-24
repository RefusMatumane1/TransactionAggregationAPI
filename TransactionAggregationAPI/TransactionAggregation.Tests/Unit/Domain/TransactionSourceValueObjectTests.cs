using FluentAssertions;
using Modules.Transactions.Domain.Common.ValueObjects;
using SharedKernel.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class TransactionSourceValueObjectTests
{

    [Fact]
    public void Create_WithValidParameters_SetsAllProperties()
    {
        var source = TransactionSource.Create("Bank A", "EXT-123");

        source.Name.Should().Be("Bank A");
        source.ExternalId.Should().Be("EXT-123");
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
    public void ToString_ReturnsNameAndExternalIdFormatted()
    {
        var source = TransactionSource.Create("Bank A", "EXT-123");

        source.ToString().Should().Be("Bank A (EXT-123)");
    }
}