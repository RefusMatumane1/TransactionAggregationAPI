using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Behaviors;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Events.Transaction;

namespace TransactionAggregation.Application.Features.Transactions.Commands.SyncTransactions
{
    public sealed record SyncTransactionsCommand : IRequest<Result<SyncTransactionsResult>>, ICommandBase, IIdempotentRequest
    {
        public required Guid CustomerId { get; init; }
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
        public string? IdempotencyKey { get; init; }
    }

    public sealed record SyncTransactionsResult
    {
        public int TotalRetrieved { get; init; }
        public int NewTransactions { get; init; }
        public int Duplicates { get; init; }
        public int FailedSources { get; init; }
        public IReadOnlyList<SourceResult> SourceResults { get; init; } = new List<SourceResult>();
        public TimeSpan Duration { get; init; }
        public DateTime SyncedAt { get; init; }
    }

    public sealed record SourceResult
    {
        public string SourceName { get; init; } = null!;
        public int TransactionsFound { get; init; }
        public int TransactionsAdded { get; init; }
        public bool IsSuccess { get; init; }
        public string? Error { get; init; }
        public TimeSpan Duration { get; init; }
    }

    public sealed class SyncTransactionsCommandHandler : IRequestHandler<SyncTransactionsCommand, Result<SyncTransactionsResult>>
    {
        private readonly ITransactionAggregator _aggregator;
        private readonly IApplicationDbContext _context;
        private readonly ILogger<SyncTransactionsCommandHandler> _logger;
        private readonly IPublisher _publisher;
        private readonly IMapper _mapper;

        public SyncTransactionsCommandHandler(
            ITransactionAggregator aggregator,
            IApplicationDbContext context,
            ILogger<SyncTransactionsCommandHandler> logger,
            IPublisher publisher,
            IMapper mapper)
        {
            _aggregator = aggregator;
            _context = context;
            _logger = logger;
            _publisher = publisher;
            _mapper = mapper;
        }

        public async Task<Result<SyncTransactionsResult>> Handle(
            SyncTransactionsCommand request,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Aggregate from all sources first so we know what's incoming.
                var aggregationResult = await _aggregator.AggregateCustomerTransactionsAsync(
                    request.CustomerId,
                    request.FromDate,
                    request.ToDate,
                    cancellationToken);

                // Check globally which of the *incoming* ExternalIds already exist in the DB.
                // Scoping to the current customer is not enough: IX_Transactions_SourceExternalId
                // is a global unique index, so a bank that reuses ExternalIds across customers
                // would still cause a 23505 violation on the second customer's sync.
                // Querying only the incoming IDs keeps the result set small regardless of
                // how many total transactions are in the database.
                var incomingExternalIds = aggregationResult.Transactions
                    .Select(t => t.Source.ExternalId)
                    .ToHashSet();

                var alreadyExistingIds = await _context.Transactions
                    .Where(t => incomingExternalIds.Contains(t.Source.ExternalId))
                    .Select(t => t.Source.ExternalId)
                    .ToHashSetAsync(cancellationToken);

                var newTransactions = aggregationResult.Transactions
                    .Where(t => !alreadyExistingIds.Contains(t.Source.ExternalId))
                    .ToList();

                // Save new transactions
                if (newTransactions.Any())
                {
                    try
                    {
                        await _context.Transactions.AddRangeAsync(newTransactions, cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("23505") == true)
                    {
                        // A concurrent sync request raced between our existence check and our insert.
                        // The transactions are already persisted — treat as idempotent success.
                        _logger.LogWarning(
                            "Duplicate ExternalId on sync for customer {CustomerId} — already inserted by a concurrent request",
                            request.CustomerId);
                        newTransactions = [];
                    }

                    foreach (var transaction in newTransactions)
                    {
                        await _publisher.Publish(
                            new TransactionSyncedEvent(transaction),
                            cancellationToken);
                    }
                }

                var sourceResults = aggregationResult.SourceResults
                    .Select(s => new SourceResult
                    {
                        SourceName = s.SourceName,
                        TransactionsFound = s.TransactionsFound,
                        TransactionsAdded = newTransactions.Count(t => t.Source.Name == s.SourceName),
                        IsSuccess = s.IsSuccess,
                        Error = s.Error,
                        Duration = s.Duration
                    })
                    .ToList();

                var result = new SyncTransactionsResult
                {
                    TotalRetrieved = aggregationResult.Transactions.Count,
                    NewTransactions = newTransactions.Count,
                    Duplicates = aggregationResult.Transactions.Count - newTransactions.Count,
                    FailedSources = aggregationResult.FailedSourceCount,
                    SourceResults = sourceResults,
                    Duration = stopwatch.Elapsed,
                    SyncedAt = DateTime.UtcNow
                };

                _logger.LogInformation(
                    "Synced {NewTransactions} new transactions for customer {CustomerId}. Total: {Total}, Duration: {Duration}ms",
                    result.NewTransactions,
                    request.CustomerId,
                    result.TotalRetrieved,
                    result.Duration.TotalMilliseconds);

                return Result<SyncTransactionsResult>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync transactions for customer {CustomerId}", request.CustomerId);
                return Result.Failure<SyncTransactionsResult>(
                    Error.Failure("SyncFailed", $"Failed to sync transactions: {ex.Message}"));
            }
        }
    }
}
