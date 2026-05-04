using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Infrastructure.Providers
{
    /// <summary>
    /// A second mock transaction source backed by a static in-memory dataset.
    /// Represents a second financial institution (e.g. a digital wallet provider).
    /// </summary>
    public class StaticDataTransactionSource : ITransactionSource
    {
        private static readonly ExternalTransactionDTO[] StaticTransactions =
        {
            new() { Id = "static-001", Amount = 2500.00m,  Currency = "ZAR", Description = "salary deposit",         Category = "", Date = DateTime.UtcNow.AddDays(-28) },
            new() { Id = "static-002", Amount = -150.00m,  Currency = "ZAR", Description = "grocery store",          Category = "", Date = DateTime.UtcNow.AddDays(-25) },
            new() { Id = "static-003", Amount = -19.99m,   Currency = "ZAR", Description = "netflix subscription",   Category = "", Date = DateTime.UtcNow.AddDays(-22) },
            new() { Id = "static-004", Amount = -45.00m,   Currency = "ZAR", Description = "uber ride",              Category = "", Date = DateTime.UtcNow.AddDays(-20) },
            new() { Id = "static-005", Amount = -320.00m,  Currency = "ZAR", Description = "electric bill",          Category = "", Date = DateTime.UtcNow.AddDays(-18) },
            new() { Id = "static-006", Amount = -89.50m,   Currency = "ZAR", Description = "amazon purchase",        Category = "", Date = DateTime.UtcNow.AddDays(-15) },
            new() { Id = "static-007", Amount = -35.00m,   Currency = "ZAR", Description = "restaurant dining",      Category = "", Date = DateTime.UtcNow.AddDays(-12) },
            new() { Id = "static-008", Amount = 500.00m,   Currency = "ZAR", Description = "freelance payment",      Category = "", Date = DateTime.UtcNow.AddDays(-10) },
            new() { Id = "static-009", Amount = -9.99m,    Currency = "ZAR", Description = "spotify subscription",   Category = "", Date = DateTime.UtcNow.AddDays(-8)  },
            new() { Id = "static-010", Amount = -250.00m,  Currency = "ZAR", Description = "rent payment",           Category = "", Date = DateTime.UtcNow.AddDays(-5)  },
            new() { Id = "static-011", Amount = -60.00m,   Currency = "ZAR", Description = "water bill",             Category = "", Date = DateTime.UtcNow.AddDays(-3)  },
            new() { Id = "static-012", Amount = -22.00m,   Currency = "ZAR", Description = "starbucks coffee",       Category = "", Date = DateTime.UtcNow.AddDays(-1)  },
        };

        private readonly ILogger<StaticDataTransactionSource> _logger;

        public string SourceName => "DigitalWallet";

        public StaticDataTransactionSource(ILogger<StaticDataTransactionSource> logger)
        {
            _logger = logger;
        }

        public async Task<IReadOnlyList<ExternalTransactionDTO>> GetTransactionsAsync(
            CustomerId customerId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken); // minimal latency

            var filtered = StaticTransactions
                .Where(t => t.Date >= fromDate && t.Date <= toDate)
                .ToList();

            _logger.LogDebug(
                "DigitalWallet returned {Count} transactions for customer {CustomerId}",
                filtered.Count, customerId.Value);

            return filtered;
        }
    }
}
