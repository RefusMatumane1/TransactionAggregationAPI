using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.Interfaces
{
    /// <summary>
    /// Service for validating transactions against business rules
    /// </summary>
    public interface ITransactionValidator
    {
        Task<ValidationResult> ValidateTransactionAsync(
            Money amount,
            string description,
            TransactionSource source,
            DateTime date,
            CancellationToken cancellationToken = default);

        Task<ValidationResult> ValidateBatchAsync(
            IEnumerable<Transaction> transactions,
            CancellationToken cancellationToken = default);

        Task<bool> IsDuplicateAsync(
            string externalId,
            TransactionSource source,
            CancellationToken cancellationToken = default);

        Task<bool> IsWithinLimitAsync(
            Guid customerId,
            Money amount,
            TransactionCategory category,
            CancellationToken cancellationToken = default);
    }

    public record ValidationResult
    {
        public bool IsValid { get; init; }
        public List<ValidationError> Errors { get; init; } = new();
        public List<ValidationWarning> Warnings { get; init; } = new();

        public static ValidationResult Success() => new() { IsValid = true };
        public static ValidationResult Failure(string errorCode, string message) =>
            new() { IsValid = false, Errors = { new ValidationError(errorCode, message) } };
    }

    public record ValidationError(string Code, string Message);
    public record ValidationWarning(string Code, string Message);
}
