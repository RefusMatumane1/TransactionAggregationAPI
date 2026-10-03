using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Application.DTOs;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Domain;
using SharedKernel.Common.Models;

namespace Modules.Audit.Application.Features.SearchAuditEvents
{
    public sealed record SearchAuditEventsQuery : IQuery<CursorPage<AuditEventDto>>
    {
        public string? Channel { get; init; }
        public string? SourceName { get; init; }
        public string? EventType { get; init; }
        public string? ExternalAccountId { get; init; }
        public Guid? InboxMessageId { get; init; }
        public Guid? TransactionId { get; init; }
        public string? ExternalTransactionId { get; init; }
        public string? Actor { get; init; }
        public DateTime? From { get; init; }
        public DateTime? To { get; init; }
        public string? Cursor { get; init; }
        public int PageSize { get; init; } = 50;
        public bool IncludeTotal { get; init; }
    }

    public static class AuditEventSort
    {
        public static readonly KeysetSort<AuditEvent> NewestFirst =
            new KeysetSort<AuditEvent, DateTime, Guid>(
                "occurredAt", e => e.OccurredAt, e => e.Id, id => id, id => id, KeyCodecs.UtcDateTime);
    }

    public sealed class SearchAuditEventsQueryValidator : AbstractValidator<SearchAuditEventsQuery>
    {
        public const int MaxPageSize = 200;

        public SearchAuditEventsQueryValidator()
        {
            RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
            RuleFor(x => x.Cursor).MustBeACursorFor(AuditEventSort.NewestFirst, descending: true);
            RuleFor(x => x)
                .Must(x => x.From is null || x.To is null || x.From <= x.To)
                .WithMessage("'From' must be on or before 'To'.");
        }
    }

    internal sealed class SearchAuditEventsQueryHandler(IAuditDbContext context, IKeysetPaginator paginator)
        : IQueryHandler<SearchAuditEventsQuery, CursorPage<AuditEventDto>>
    {
        public async Task<Result<CursorPage<AuditEventDto>>> Handle(SearchAuditEventsQuery request, CancellationToken cancellationToken)
        {
            var query = context.AuditEvents.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Channel))
                query = query.Where(e => e.Channel == request.Channel);
            if (!string.IsNullOrWhiteSpace(request.SourceName))
                query = query.Where(e => e.SourceName == request.SourceName);
            if (!string.IsNullOrWhiteSpace(request.EventType))
                query = query.Where(e => e.EventType == request.EventType);
            if (!string.IsNullOrWhiteSpace(request.ExternalAccountId))
                query = query.Where(e => e.ExternalAccountId == request.ExternalAccountId);
            if (request.InboxMessageId is { } inboxMessageId)
                query = query.Where(e => e.InboxMessageId == inboxMessageId);
            if (request.TransactionId is { } transactionId)
                query = query.Where(e => e.TransactionId == transactionId);
            if (!string.IsNullOrWhiteSpace(request.ExternalTransactionId))
                query = query.Where(e => e.ExternalTransactionId == request.ExternalTransactionId);
            if (!string.IsNullOrWhiteSpace(request.Actor))
                query = query.Where(e => e.Actor == request.Actor);
            if (request.From is { } from)
                query = query.Where(e => e.OccurredAt >= from);
            if (request.To is { } to)
                query = query.Where(e => e.OccurredAt <= to);

            BoundedCount? total = request.IncludeTotal
                ? await BoundedCount.OfAsync(query, (q, ct) => q.CountAsync(ct), cancellationToken)
                : null;

            var sort = AuditEventSort.NewestFirst;
            var window = PageCursor.TryDecode(request.Cursor, out var cursor)
                ? sort.After(query, paginator, cursor!)
                : query;

            var events = await sort.Order(window, descending: true)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            return Result.Success(sort.ToPage(events, request.PageSize, descending: true, AuditEventDto.From, total));
        }
    }
}