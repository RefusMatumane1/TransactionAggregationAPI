using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a domain rule is violated
    /// </summary>
    public class DomainException : Exception
    {
        public string ErrorCode { get; }
        public string? DetailedMessage { get; }
        public DateTime OccurredAt { get; }

        public DomainException()
            : base("A domain rule was violated")
        {
            ErrorCode = "DOMAIN_RULE_VIOLATION";
            OccurredAt = DateTime.UtcNow;
        }

        public DomainException(string message)
            : base(message)
        {
            ErrorCode = "DOMAIN_RULE_VIOLATION";
            OccurredAt = DateTime.UtcNow;
        }

        public DomainException(string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = "DOMAIN_RULE_VIOLATION";
            OccurredAt = DateTime.UtcNow;
        }

        public DomainException(string errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
            OccurredAt = DateTime.UtcNow;
        }

        public DomainException(string errorCode, string message, string detailedMessage)
            : base(message)
        {
            ErrorCode = errorCode;
            DetailedMessage = detailedMessage;
            OccurredAt = DateTime.UtcNow;
        }

        // Common domain exceptions factory methods
        public static DomainException InvalidAmount(string reason) =>
            new("INVALID_AMOUNT", $"Invalid transaction amount: {reason}");

        public static DomainException InvalidDate(string reason) =>
            new("INVALID_DATE", $"Invalid transaction date: {reason}");

        public static DomainException InvalidStatusTransition(TransactionStatus from, TransactionStatus to) =>
            new("INVALID_STATUS_TRANSITION", $"Cannot transition from {from} to {to}");

        public static DomainException TransactionNotFound(Guid transactionId) =>
            new("TRANSACTION_NOT_FOUND", $"Transaction with ID {transactionId} was not found");

        public static DomainException DuplicateTransaction(string externalId) =>
            new("DUPLICATE_TRANSACTION", $"Transaction with external ID {externalId} already exists");

        public static DomainException InvalidCategory(string category) =>
            new("INVALID_CATEGORY", $"Transaction category '{category}' is not valid");

        public static DomainException MissingRequiredInformation(string field) =>
            new("MISSING_REQUIRED_INFO", $"Required field '{field}' is missing");
    }
}
