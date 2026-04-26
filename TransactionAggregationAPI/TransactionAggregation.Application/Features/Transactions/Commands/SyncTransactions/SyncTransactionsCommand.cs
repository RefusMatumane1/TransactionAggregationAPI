using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TransactionAggregation.Application.Common.Behaviors;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Events;
using TransactionAggregation.Domain.Events.Transaction;

namespace TransactionAggregation.Application.Features.Transactions.Commands.SyncTransactions
{
    public sealed record SyncTransactionsCommand : IRequest<Result<SyncTransactionsResult>>, ICommand, IIdempotentRequest
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
                // Get existing transaction IDs to avoid duplicates
                var existingIds = await _context.Transactions
                    .Where(t => t.CustomerId == CustomerId.CreateFrom(request.CustomerId))
                    .Select(t => t.Source.ExternalId)
                    .ToHashSetAsync(cancellationToken);

                // Aggregate from all sources
                var aggregationResult = await _aggregator.AggregateCustomerTransactionsAsync(
                    request.CustomerId,
                    request.FromDate,
                    request.ToDate,
                    cancellationToken);

                // Filter new transactions
                var newTransactions = aggregationResult
                    .Where(t => !existingIds.Contains(t.Source.ExternalId))
                    .ToList();

                // Save new transactions
                if (newTransactions.Any())
                {
                    await _context.Transactions.AddRangeAsync(newTransactions, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);

                    // Publish domain events for each new transaction
                    foreach (var transaction in newTransactions)
                    {
                        await _publisher.Publish(
                            new TransactionSyncedEvent(transaction),
                            cancellationToken);
                    }
                }

                var result = new SyncTransactionsResult
                {
                    TotalRetrieved = aggregationResult.Count,
                    NewTransactions = newTransactions.Count,
                    Duplicates = aggregationResult.Count - newTransactions.Count,
                    //FailedSources = aggregationResult.Count(r => !r.),
                    //SourceResults = aggregationResult.Select(x => x.Source),
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
