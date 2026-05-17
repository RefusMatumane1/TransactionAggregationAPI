using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Services
{
    public class TransactionValidator : ITransactionValidator
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<TransactionValidator> _logger;
        private readonly TransactionValidationOptions _options;

        public TransactionValidator(
            IApplicationDbContext context,
            ILogger<TransactionValidator> logger,
            IOptions<TransactionValidationOptions> options)
        {
            _context = context;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<ValidationResult> ValidateTransactionAsync(
            Money amount,
            string description,
            TransactionSource source,
            DateTime date,
            CancellationToken cancellationToken = default)
        {
            var errors = new List<ValidationError>();
            var warnings = new List<ValidationWarning>();

            // Validate amount
            if (amount.Amount == 0)
                errors.Add(new ValidationError("ZERO_AMOUNT", "Transaction amount cannot be zero"));

            if (Math.Abs(amount.Amount) > _options.MaxTransactionAmount)
                errors.Add(new ValidationError("AMOUNT_EXCEEDS_LIMIT",
                    $"Transaction amount {amount.Formatted} exceeds maximum allowed {_options.MaxTransactionAmount:C}"));

            // Validate date
            if (date > DateTime.UtcNow)
                errors.Add(new ValidationError("FUTURE_DATE", "Transaction cannot have a future date"));

            if (date < DateTime.UtcNow.AddMonths(-_options.MaxTransactionAgeMonths))
                warnings.Add(new ValidationWarning("OLD_TRANSACTION",
                    $"Transaction is older than {_options.MaxTransactionAgeMonths} months"));

            // Validate description
            if (string.IsNullOrWhiteSpace(description))
                errors.Add(new ValidationError("EMPTY_DESCRIPTION", "Transaction description is required"));

            if (description.Length > 500)
                warnings.Add(new ValidationWarning("LONG_DESCRIPTION",
                    $"Description length {description.Length} exceeds recommended maximum"));

            // Validate source
            if (string.IsNullOrWhiteSpace(source.Name))
                errors.Add(new ValidationError("INVALID_SOURCE", "Transaction source name is required"));

            // Check for duplicates
            if (await IsDuplicateAsync(source.ExternalId, source, cancellationToken))
                errors.Add(new ValidationError("DUPLICATE_TRANSACTION",
                    $"Transaction with external ID {source.ExternalId} already exists"));

            // Check customer limits
            if (!await IsWithinLimitAsync(Guid.Empty, amount, TransactionCategory.Uncategorized, cancellationToken))
                warnings.Add(new ValidationWarning("EXCEEDS_DAILY_LIMIT",
                    "This transaction may exceed customer daily spending limits"));

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        public async Task<ValidationResult> ValidateBatchAsync(
            IEnumerable<Transaction> transactions,
            CancellationToken cancellationToken = default)
        {
            var allErrors = new List<ValidationError>();
            var allWarnings = new List<ValidationWarning>();

            foreach (var transaction in transactions)
            {
                var result = await ValidateTransactionAsync(
                    transaction.Amount,
                    transaction.Description,
                    transaction.Source,
                    transaction.Date,
                    cancellationToken);

                allErrors.AddRange(result.Errors);
                allWarnings.AddRange(result.Warnings);
            }

            return new ValidationResult
            {
                IsValid = allErrors.Count == 0,
                Errors = allErrors.DistinctBy(e => e.Code).ToList(),
                Warnings = allWarnings.DistinctBy(w => w.Code).ToList()
            };
        }

        public async Task<bool> IsDuplicateAsync(
            string externalId,
            TransactionSource source,
            CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .AnyAsync(t => t.Source.ExternalId == externalId &&
                              t.Source.Name == source.Name,
                          cancellationToken);
        }

        public async Task<bool> IsWithinLimitAsync(
            Guid customerId,
            Money amount,
            TransactionCategory category,
            CancellationToken cancellationToken = default)
        {
            var today = DateTime.UtcNow.Date;

            var dailySpending = await _context.Transactions
                .Where(t => t.CustomerId == CustomerId.CreateFrom(customerId) &&
                           t.Date >= today &&
                           t.Amount.Amount < 0)
                .SumAsync(t => -t.Amount.Amount, cancellationToken);

            var newTotal = dailySpending + amount.AbsoluteAmount;
            return newTotal <= _options.DailySpendingLimit;
        }
    }

    public class TransactionValidationOptions
    {
        public decimal MaxTransactionAmount { get; set; } = 50000;
        public int MaxTransactionAgeMonths { get; set; } = 36;
        public decimal DailySpendingLimit { get; set; } = 10000;
    }
}
