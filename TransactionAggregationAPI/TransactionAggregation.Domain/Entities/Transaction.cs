using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Events;
using TransactionAggregation.Domain.Events.Transaction;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Domain.Entities
{
    public sealed class Transaction : BaseEntity
    {
        private Transaction() { } // EF Core

        private Transaction(
            TransactionId id,
            CustomerId customerId,
            AccountId? accountId,
            Money amount,
            string description,
            TransactionCategory category,
            TransactionSource source,
            DateTime date)
        {
            Id = id;
            CustomerId = customerId;
            AccountId = accountId;
            Amount = amount;
            Description = description;
            Category = category;
            Source = source;
            Date = date;
            Status = TransactionStatus.Pending;
            AddDomainEvent(new TransactionCreatedDomainEvent(this));
        }

        // Properties
        public TransactionId Id { get; private set; }
        public CustomerId CustomerId { get; private set; }
        public AccountId? AccountId { get; private set; }
        public Money Amount { get; private set; }
        public string Description { get; private set; }
        public TransactionCategory Category { get; private set; }
        public TransactionSource Source { get; private set; }
        public DateTime? ApprovedAt { get; private set; }
        public string? ApprovedBy { get; private set; }
        public DateTime Date { get; private set; }
        public TransactionStatus Status { get; private set; }
        public Dictionary<string, string> Metadata { get; private set; } = new();


        public static Transaction Create(
            CustomerId customerId,
            Money amount,
            string description,
            TransactionCategory category,
            TransactionSource source,
            AccountId? accountId = null,
            DateTime? date = null)
        {
            return new Transaction(
                TransactionId.Create(),
                customerId,
                accountId,
                amount,
                description,
                category,
                source,
                date ?? DateTime.UtcNow);
        }


        public void Categorize(TransactionCategory newCategory)
        {
            if (Category == newCategory)
                return;

            var oldCategory = Category;
            Category = newCategory;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new TransactionCategorizedDomainEvent(this, oldCategory, newCategory));
        }

        public void Approve(string approvedBy = "System", string? notes = null)
        {
            if (Status == TransactionStatus.Approved)
                return;

            if (Status == TransactionStatus.Rejected)
                throw new DomainException("Cannot approve a rejected transaction");

            var oldStatus = Status;
            Status = TransactionStatus.Approved;
            ApprovedAt = DateTime.UtcNow;
            ApprovedBy = approvedBy;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new TransactionApprovedDomainEvent(this, oldStatus, approvedBy));
        }

        //UpdateStatus
        public void UpdateStatus(TransactionStatus newStatus, string reason)
        {
            if (Status == newStatus)
                return;

            var oldStatus = Status;
            Status = newStatus;
            UpdatedAt = DateTime.UtcNow;
           // AddDomainEvent(new TransactionStatusUpdatedDomainEvent(this, oldStatus, newStatus, reason));
        }

        public void Reject(string reason, string rejectedBy = "System")
        {
            if (Status == TransactionStatus.Rejected)
                return;

            if (Status == TransactionStatus.Approved)
                throw new DomainException("Cannot reject an already approved transaction");

            var oldStatus = Status;
            Status = TransactionStatus.Rejected;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new TransactionRejectedDomainEvent(this, reason, rejectedBy, oldStatus));
        }

        public void Flag(string reason)
        {
            if (Status == TransactionStatus.Flagged)
                return;

            var oldStatus = Status;
            Status = TransactionStatus.Flagged;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new TransactionFlaggedDomainEvent(this, reason, oldStatus));
        }

        public void Refund(string reason)
        {
            if (Status == TransactionStatus.Refunded)
                return;

            if (Status != TransactionStatus.Settled && Status != TransactionStatus.Approved)
                throw new DomainException($"Cannot refund a transaction with status {Status}");

            var oldStatus = Status;
            Status = TransactionStatus.Refunded;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new TransactionRefundedDomainEvent(this, reason, oldStatus));
        }
        public void AddMetadata(string key, string value)
        {
            if (Metadata.ContainsKey(key))
                Metadata[key] = value;
            else
                Metadata.Add(key, value);

            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new TransactionMetadataAddedDomainEvent(this, key, value));
        }

        public void RemoveMetadata(string key)
        {
            if (Metadata.Remove(key))
            {
                UpdatedAt = DateTime.UtcNow;
                AddDomainEvent(new TransactionMetadataRemovedDomainEvent(this, key));
            }
        }

        public bool IsPending => Status == TransactionStatus.Pending;
        public bool IsApproved => Status == TransactionStatus.Approved;
        public bool IsSettled => Status == TransactionStatus.Settled;
        public bool IsExpense => Amount.IsExpense;
        public bool IsIncome => Amount.IsIncome;
        public decimal AbsoluteAmount => Amount.AbsoluteAmount;
    }
}