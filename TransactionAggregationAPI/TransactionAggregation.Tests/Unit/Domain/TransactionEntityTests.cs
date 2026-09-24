using FluentAssertions;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Common.ValueObjects;
using SharedKernel.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class TransactionEntityTests
{
    private static Transaction CreatePending(decimal amount = -100m) =>
        Transaction.Create(
            CustomerId.Create(),
            Money.Create(amount, "ZAR"),
            "test transaction",
            TransactionCategory.Uncategorized,
            TransactionSource.Create("TestSource", Guid.NewGuid().ToString()));

    [Fact]
    public void Categorize_ChangesCategory_AndRaisesEvent()
    {
        var tx = CreatePending();
        tx.Categorize(TransactionCategory.Groceries);

        tx.Category.Should().Be(TransactionCategory.Groceries);
        tx.DomainEvents.Should().Contain(e => e.GetType().Name == "TransactionCategorizedDomainEvent");
    }

    [Fact]
    public void Categorize_ToSameCategory_IsNoOp()
    {
        var tx = CreatePending();
        tx.Categorize(TransactionCategory.Uncategorized);

        tx.DomainEvents.Should().NotContain(e => e.GetType().Name == "TransactionCategorizedDomainEvent");
    }

    [Fact]
    public void AddMetadata_StoresKeyValue()
    {
        var tx = CreatePending();
        tx.AddMetadata("invoiceId", "INV-001");

        tx.Metadata.Should().ContainKey("invoiceId").WhoseValue.Should().Be("INV-001");
    }

    [Fact]
    public void IsExpense_ForNegativeAmount_IsTrue()
    {
        var tx = CreatePending(-100m);
        tx.IsExpense.Should().BeTrue();
        tx.IsIncome.Should().BeFalse();
    }

    [Fact]
    public void IsIncome_ForPositiveAmount_IsTrue()
    {
        var tx = CreatePending(500m);
        tx.IsIncome.Should().BeTrue();
        tx.IsExpense.Should().BeFalse();
    }

    [Fact]
    public void Settle_Pending_BecomesSettled()
    {
        var tx = CreatePending();

        tx.Settle();

        tx.Status.Should().Be(TransactionStatus.Settled);
        tx.Amount.Amount.Should().Be(-100m);
    }

    [Fact]
    public void Settle_WithPostedAmountAndDate_TakesTheBanksFigures()
    {
        var tx = CreatePending(-100m);
        var postedDate = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);

        tx.Settle(Money.Create(-112.50m, "ZAR"), postedDate);

        tx.Amount.Amount.Should().Be(-112.50m);
        tx.Date.Should().Be(postedDate);
    }

    [Fact]
    public void Settle_AlreadySettled_IsANoOp()
    {
        var tx = CreatePending(-100m);
        tx.Settle();

        tx.Settle(Money.Create(-999m, "ZAR"));

        tx.Amount.Amount.Should().Be(-100m, "a settled transaction's booked amount must not be overwritten by a replay");
    }

    [Theory]
    [InlineData(TransactionStatus.Rejected)]
    [InlineData(TransactionStatus.Cancelled)]
    [InlineData(TransactionStatus.Refunded)]
    public void Settle_VoidedTransaction_Throws(TransactionStatus status)
    {
        var tx = CreatePending();
        tx.UpdateStatus(status);

        var act = () => tx.Settle();

        act.Should().Throw<DomainException>();
    }
}