using FluentAssertions;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class AccountEntityTests
{
    private static CustomerId NewCustomerId() => CustomerId.Create();

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidParameters_SetsAllProperties()
    {
        var customerId = NewCustomerId();
        var account = Account.Create(customerId, "ACC-001", "Main Cheque", AccountType.Checking, "ZAR");

        account.CustomerId.Should().Be(customerId);
        account.AccountNumber.Should().Be("ACC-001");
        account.AccountName.Should().Be("Main Cheque");
        account.AccountType.Should().Be(AccountType.Checking);
        account.Balance.Should().Be(0m);
        account.Currency.Should().Be("ZAR");
        account.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultCurrency_IsZAR()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Savings);

        account.Currency.Should().Be("ZAR");
    }

    [Fact]
    public void Create_NormalisesCurrencyToUpperCase()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking, "usd");

        account.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromAccountNumber()
    {
        var account = Account.Create(NewCustomerId(), "  ACC-001  ", "My Account", AccountType.Checking);

        account.AccountNumber.Should().Be("ACC-001");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromAccountName()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "  My Account  ", AccountType.Checking);

        account.AccountName.Should().Be("My Account");
    }

    [Fact]
    public void Create_WithEmptyAccountNumber_ThrowsDomainException()
    {
        var act = () => Account.Create(NewCustomerId(), "", "My Account", AccountType.Checking);

        act.Should().Throw<DomainException>()
           .WithMessage("*Account number*");
    }

    [Fact]
    public void Create_WithWhitespaceAccountNumber_ThrowsDomainException()
    {
        var act = () => Account.Create(NewCustomerId(), "   ", "My Account", AccountType.Checking);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithEmptyAccountName_ThrowsDomainException()
    {
        var act = () => Account.Create(NewCustomerId(), "ACC-001", "", AccountType.Checking);

        act.Should().Throw<DomainException>()
           .WithMessage("*Account name*");
    }

    [Fact]
    public void Create_WithInvalidCurrencyTooShort_ThrowsDomainException()
    {
        var act = () => Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking, "ZA");

        act.Should().Throw<DomainException>()
           .WithMessage("*ISO 4217*");
    }

    [Fact]
    public void Create_WithInvalidCurrencyTooLong_ThrowsDomainException()
    {
        var act = () => Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking, "ZARR");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithEmptyCurrency_ThrowsDomainException()
    {
        var act = () => Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking, "");

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(AccountType.Checking)]
    [InlineData(AccountType.Savings)]
    [InlineData(AccountType.CreditCard)]
    [InlineData(AccountType.Investment)]
    [InlineData(AccountType.Loan)]
    public void Create_AllAccountTypes_Succeed(AccountType accountType)
    {
        var act = () => Account.Create(NewCustomerId(), "ACC-001", "My Account", accountType);

        act.Should().NotThrow();
    }

    // ── Credit ────────────────────────────────────────────────────────────────

    [Fact]
    public void Credit_PositiveAmount_IncreasesBalance()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);

        account.Credit(500m);

        account.Balance.Should().Be(500m);
    }

    [Fact]
    public void Credit_MultipleCredits_AccumulatesBalance()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);

        account.Credit(200m);
        account.Credit(300m);

        account.Balance.Should().Be(500m);
    }

    [Fact]
    public void Credit_ZeroAmount_ThrowsDomainException()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);

        account.Invoking(a => a.Credit(0m))
               .Should().Throw<DomainException>()
               .WithMessage("*positive*");
    }

    [Fact]
    public void Credit_NegativeAmount_ThrowsDomainException()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);

        account.Invoking(a => a.Credit(-100m))
               .Should().Throw<DomainException>();
    }

    [Fact]
    public void Credit_OnInactiveAccount_ThrowsDomainException()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);
        account.Deactivate();

        account.Invoking(a => a.Credit(100m))
               .Should().Throw<DomainException>()
               .WithMessage("*inactive*");
    }

    // ── Debit ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Debit_PositiveAmount_DecreasesBalance()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);
        account.Credit(500m);

        account.Debit(200m);

        account.Balance.Should().Be(300m);
    }

    [Fact]
    public void Debit_ZeroAmount_ThrowsDomainException()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);

        account.Invoking(a => a.Debit(0m))
               .Should().Throw<DomainException>()
               .WithMessage("*positive*");
    }

    [Fact]
    public void Debit_NegativeAmount_ThrowsDomainException()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);

        account.Invoking(a => a.Debit(-100m))
               .Should().Throw<DomainException>();
    }

    [Fact]
    public void Debit_OnInactiveAccount_ThrowsDomainException()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);
        account.Credit(500m);
        account.Deactivate();

        account.Invoking(a => a.Debit(100m))
               .Should().Throw<DomainException>()
               .WithMessage("*inactive*");
    }

    [Fact]
    public void Debit_AllowsNegativeBalance_WhenSufficientCreditNotEnforced()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.CreditCard);
        account.Credit(100m);

        account.Debit(200m);

        account.Balance.Should().Be(-100m);
    }

    // ── Deactivate / Reactivate ───────────────────────────────────────────────

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);

        account.Deactivate();

        account.IsActive.Should().BeFalse();
        account.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reactivate_SetsIsActiveTrue()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);
        account.Deactivate();

        account.Reactivate();

        account.IsActive.Should().BeTrue();
        account.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Deactivate_ThenReactivate_AllowsCredits()
    {
        var account = Account.Create(NewCustomerId(), "ACC-001", "My Account", AccountType.Checking);
        account.Deactivate();
        account.Reactivate();

        account.Invoking(a => a.Credit(100m)).Should().NotThrow();
    }
}
