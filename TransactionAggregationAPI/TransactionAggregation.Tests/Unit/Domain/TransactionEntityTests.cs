using FluentAssertions;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Exceptions;
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

    // ── Categorize ────────────────────────────────────────────────────────────

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

        // No event raised for no-op
        tx.DomainEvents.Should().NotContain(e => e.GetType().Name == "TransactionCategorizedDomainEvent");
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_FromPending_SetsStatusAndRaisesEvent()
    {
        var tx = CreatePending();
        tx.Approve("reviewer@bank.com");

        tx.Status.Should().Be(TransactionStatus.Approved);
        tx.ApprovedBy.Should().Be("reviewer@bank.com");
        tx.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_IsNoOp()
    {
        var tx = CreatePending();
        tx.Approve();
        var eventCountBefore = tx.DomainEvents.Count;

        tx.Approve(); // second call

        tx.DomainEvents.Count.Should().Be(eventCountBefore);
    }

    [Fact]
    public void Approve_WhenRejected_Throws()
    {
        var tx = CreatePending();
        tx.Reject("fraud", "system");

        tx.Invoking(t => t.Approve())
          .Should().Throw<DomainException>();
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_FromPending_SetsRejectedStatus()
    {
        var tx = CreatePending();
        tx.Reject("insufficient funds");

        tx.Status.Should().Be(TransactionStatus.Rejected);
    }

    [Fact]
    public void Reject_WhenAlreadyApproved_Throws()
    {
        var tx = CreatePending();
        tx.Approve();

        tx.Invoking(t => t.Reject("late rejection"))
          .Should().Throw<DomainException>();
    }

    // ── Refund ────────────────────────────────────────────────────────────────

    [Fact]
    public void Refund_WhenApproved_SetsRefundedStatus()
    {
        var tx = CreatePending();
        tx.Approve();
        tx.Refund("customer request");

        tx.Status.Should().Be(TransactionStatus.Refunded);
    }

    [Fact]
    public void Refund_WhenStillPending_Throws()
    {
        var tx = CreatePending();

        tx.Invoking(t => t.Refund("too early"))
          .Should().Throw<DomainException>();
    }

    // ── Metadata ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddMetadata_StoresKeyValue()
    {
        var tx = CreatePending();
        tx.AddMetadata("invoiceId", "INV-001");

        tx.Metadata.Should().ContainKey("invoiceId").WhoseValue.Should().Be("INV-001");
    }

    [Fact]
    public void RemoveMetadata_RemovesExistingKey()
    {
        var tx = CreatePending();
        tx.AddMetadata("invoiceId", "INV-001");
        tx.RemoveMetadata("invoiceId");

        tx.Metadata.Should().NotContainKey("invoiceId");
    }

    // ── Helper properties ─────────────────────────────────────────────────────

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
}
