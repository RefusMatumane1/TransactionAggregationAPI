using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Domain.Events.Transaction;
using SharedKernel.Common;
using SharedKernel.Common.ValueObjects;
using SharedKernel.Exceptions;

namespace Modules.Transactions.Domain.Entities
{
    public sealed class Transaction : BaseEntity
    {
        private Transaction() { }

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

        public TransactionId Id { get; private set; } = null!;
        public CustomerId CustomerId { get; private set; } = null!;
        public AccountId? AccountId { get; private set; }
        public Money Amount { get; private set; } = null!;
        public string Description { get; private set; } = null!;
        public TransactionCategory Category { get; private set; }
        public TransactionSource Source { get; private set; } = null!;
        public DateTime Date { get; private set; }
        public TransactionStatus Status { get; private set; }
        public Dictionary<string, string> Metadata { get; private set; } = new();

        /// <summary>
        /// Optimistic-concurrency token (mapped to Postgres' xmin system column). Ingestion
        /// settling a row and the expiry job expiring it can race; without this the later
        /// write silently wins, and an expiry could overwrite a real posting.
        /// </summary>
        public uint Version { get; private set; }

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

        public void Categorize(TransactionCategory newCategory, bool isAuto = false)
        {
            if (Category == newCategory)
                return;

            var oldCategory = Category;
            Category = newCategory;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new TransactionCategorizedDomainEvent(this, oldCategory, newCategory, isAutoCategorized: isAuto));
        }

        public void UpdateStatus(TransactionStatus newStatus)
        {
            if (Status == newStatus)
                return;

            Status = newStatus;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// The bank has posted (booked) this transaction. A posting can differ from the pending
        /// authorisation it replaces — tips, currency conversion, a later value date — so the
        /// bank's posted amount/date overwrite ours when supplied. Idempotent for an already
        /// settled transaction; a voided one (rejected/cancelled/refunded) can't be settled.
        /// An Expired one can: expiry was only our guess that the posting would never come.
        /// </summary>
        public void Settle(Money? postedAmount = null, DateTime? postedDate = null)
        {
            if (Status == TransactionStatus.Settled)
                return;

            if (Status is TransactionStatus.Rejected or TransactionStatus.Cancelled or TransactionStatus.Refunded)
                throw new DomainException($"Cannot settle a transaction with status {Status}");

            if (postedAmount is not null)
                Amount = postedAmount;
            if (postedDate is { } date)
                Date = date;

            Status = TransactionStatus.Settled;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Gives up on a pending authorisation the bank never posted. Only a Pending
        /// transaction can expire — anything else already has a final answer.
        /// </summary>
        public void Expire()
        {
            if (Status != TransactionStatus.Pending)
                throw new DomainException($"Only a pending transaction can expire (status is {Status})");

            Status = TransactionStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// How long this has been pending as far as we know: counted from whichever is later,
        /// the bank's transaction date or when we received it — so an old authorisation that
        /// only just arrived still gets the full window to be posted.
        /// </summary>
        public DateTime PendingSince => Date > CreatedAt ? Date : CreatedAt;

        public void AddMetadata(string key, string value)
        {
            Metadata[key] = value;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsPending => Status == TransactionStatus.Pending;
        public bool IsExpense => Amount.IsExpense;
        public bool IsIncome => Amount.IsIncome;
        public decimal AbsoluteAmount => Amount.AbsoluteAmount;
    }
}