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