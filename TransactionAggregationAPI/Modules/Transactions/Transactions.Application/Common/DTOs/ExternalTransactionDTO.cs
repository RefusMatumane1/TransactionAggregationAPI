using System;

namespace Modules.Transactions.Application.Common.DTOs
{
    public record ExternalTransactionDTO
    {
        public string Id { get; init; } = null!;
        public decimal Amount { get; init; }
        public string Currency { get; init; } = null!;
        public string Description { get; init; } = null!;
        public string Category { get; init; } = null!;
        public DateTime Date { get; init; }

        /// <summary>
        /// The bank's view of the transaction: "pending" or "posted". Null means posted —
        /// senders that predate this field only ever sent booked transactions.
        /// See <see cref="BankTransactionStatus"/>.
        /// </summary>
        public string? Status { get; init; }
    }

    public static class BankTransactionStatus
    {
        public const string Pending = "pending";
        public const string Posted = "posted";

        public static bool IsValid(string? status) =>
            status is null
            || string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, Posted, StringComparison.OrdinalIgnoreCase);

        public static bool IsPending(string? status) =>
            string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase);
    }
}