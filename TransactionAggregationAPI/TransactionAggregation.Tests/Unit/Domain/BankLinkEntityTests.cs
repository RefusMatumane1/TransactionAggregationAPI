using FluentAssertions;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class BankLinkEntityTests
{
    private static CustomerId NewCustomerId() => CustomerId.Create();

    private static BankLink ActivatedLink(CustomerId? customerId = null)
    {
        var link = BankLink.Create(customerId ?? NewCustomerId(), Institution.FNB);
        link.Activate(
            AccountId.Create(),
            "ext-acc-1",
            "encrypted-access",
            "encrypted-refresh",
            DateTime.UtcNow.AddHours(1));
        return link;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidParameters_SetsAllProperties()
    {
        var customerId = NewCustomerId();

        var link = BankLink.Create(customerId, Institution.Capitec);

        link.CustomerId.Should().Be(customerId);
        link.Institution.Should().Be(Institution.Capitec);
        link.Status.Should().Be(BankLinkStatus.PendingAuthorization);
        link.AccountId.Should().BeNull();
        link.ExternalAccountId.Should().BeNull();
        link.EncryptedAccessToken.Should().BeNull();
        link.EncryptedRefreshToken.Should().BeNull();
        link.TokenExpiresAt.Should().BeNull();
    }

    // ── Activate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Activate_FromPendingAuthorization_SetsActiveAndTokenFields()
    {
        var link = BankLink.Create(NewCustomerId(), Institution.Absa);
        var accountId = AccountId.Create();
        var expiresAt = DateTime.UtcNow.AddHours(1);

        link.Activate(accountId, "ext-acc-1", "enc-access", "enc-refresh", expiresAt);

        link.Status.Should().Be(BankLinkStatus.Active);
        link.AccountId.Should().Be(accountId);
        link.ExternalAccountId.Should().Be("ext-acc-1");
        link.EncryptedAccessToken.Should().Be("enc-access");
        link.EncryptedRefreshToken.Should().Be("enc-refresh");
        link.TokenExpiresAt.Should().Be(expiresAt);
        link.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Activate_OnRevokedLink_ThrowsDomainException()
    {
        var link = BankLink.Create(NewCustomerId(), Institution.FNB);
        link.Revoke();

        var act = () => link.Activate(AccountId.Create(), "ext-acc-1", "enc-access", "enc-refresh", DateTime.UtcNow);

        act.Should().Throw<DomainException>()
           .WithMessage("*revoked*");
    }

    // ── UpdateTokens ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdateTokens_OnActiveLink_ReplacesTokenFieldsAndKeepsActive()
    {
        var link = ActivatedLink();
        var newExpiry = DateTime.UtcNow.AddHours(2);

        link.UpdateTokens("new-access", "new-refresh", newExpiry);

        link.EncryptedAccessToken.Should().Be("new-access");
        link.EncryptedRefreshToken.Should().Be("new-refresh");
        link.TokenExpiresAt.Should().Be(newExpiry);
        link.Status.Should().Be(BankLinkStatus.Active);
    }

    [Fact]
    public void UpdateTokens_ClearsNeedsReauthorizationStatus()
    {
        var link = ActivatedLink();
        link.MarkNeedsReauthorization();

        link.UpdateTokens("new-access", "new-refresh", DateTime.UtcNow.AddHours(1));

        link.Status.Should().Be(BankLinkStatus.Active);
    }

    [Fact]
    public void UpdateTokens_OnRevokedLink_ThrowsDomainException()
    {
        var link = ActivatedLink();
        link.Revoke();

        var act = () => link.UpdateTokens("new-access", "new-refresh", DateTime.UtcNow.AddHours(1));

        act.Should().Throw<DomainException>()
           .WithMessage("*revoked*");
    }

    // ── MarkNeedsReauthorization ──────────────────────────────────────────────

    [Fact]
    public void MarkNeedsReauthorization_OnActiveLink_SetsStatus()
    {
        var link = ActivatedLink();

        link.MarkNeedsReauthorization();

        link.Status.Should().Be(BankLinkStatus.NeedsReauthorization);
    }

    [Fact]
    public void MarkNeedsReauthorization_OnRevokedLink_IsNoOp()
    {
        var link = ActivatedLink();
        link.Revoke();

        link.MarkNeedsReauthorization();

        link.Status.Should().Be(BankLinkStatus.Revoked);
    }

    // ── ResetForReauthorization ───────────────────────────────────────────────

    [Fact]
    public void ResetForReauthorization_OnRevokedLink_ReturnsToPendingAndClearsTokens()
    {
        var link = ActivatedLink();
        link.Revoke();

        link.ResetForReauthorization();

        link.Status.Should().Be(BankLinkStatus.PendingAuthorization);
        link.EncryptedAccessToken.Should().BeNull();
        link.EncryptedRefreshToken.Should().BeNull();
        link.TokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ResetForReauthorization_OnNeedsReauthorizationLink_ReturnsToPending()
    {
        var link = ActivatedLink();
        link.MarkNeedsReauthorization();

        link.ResetForReauthorization();

        link.Status.Should().Be(BankLinkStatus.PendingAuthorization);
    }

    [Theory]
    [InlineData(BankLinkStatus.Active)]
    [InlineData(BankLinkStatus.PendingAuthorization)]
    public void ResetForReauthorization_OnActiveOrPendingLink_ThrowsDomainException(BankLinkStatus status)
    {
        var link = status == BankLinkStatus.Active
            ? ActivatedLink()
            : BankLink.Create(NewCustomerId(), Institution.FNB);

        var act = () => link.ResetForReauthorization();

        act.Should().Throw<DomainException>()
           .WithMessage("*already*");
    }

    // ── Revoke ────────────────────────────────────────────────────────────────

    [Fact]
    public void Revoke_ClearsTokensAndSetsRevokedStatus()
    {
        var link = ActivatedLink();

        link.Revoke();

        link.Status.Should().Be(BankLinkStatus.Revoked);
        link.EncryptedAccessToken.Should().BeNull();
        link.EncryptedRefreshToken.Should().BeNull();
        link.TokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Revoke_FromPendingAuthorization_Succeeds()
    {
        var link = BankLink.Create(NewCustomerId(), Institution.FNB);

        link.Revoke();

        link.Status.Should().Be(BankLinkStatus.Revoked);
    }
}
