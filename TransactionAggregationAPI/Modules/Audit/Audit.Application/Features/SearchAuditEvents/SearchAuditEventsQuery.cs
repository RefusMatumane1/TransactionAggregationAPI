using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Application.DTOs;
using Modules.Audit.Application.Persistence;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;

namespace Modules.Audit.Application.Features.SearchAuditEvents
{
    public sealed record SearchAuditEventsQuery : IQuery<AuditEventPage>
    {
        public string? Channel { get; init; }
        public string? SourceName { get; init; }
        public string? EventType { get; init; }
        public string? ExternalAccountId { get; init; }
        public Guid? InboxMessageId { get; init; }
        public Guid? CustomerId { get; init; }
        public Guid? TransactionId { get; init; }
        public string? ExternalTransactionId { get; init; }
        public DateTime? From { get; init; }
        public DateTime? To { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 50;
    }

    public sealed class SearchAuditEventsQueryValidator : AbstractValidator<SearchAuditEventsQuery>
    {
        public SearchAuditEventsQueryValidator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
            RuleFor(x => x)
                .Must(x => x.From is null || x.To is null || x.From <= x.To)
                .WithMessage("'From' must be on or before 'To'.");
        }
    }

    internal sealed class SearchAuditEventsQueryHandler(IAuditDbContext context)
        : IQueryHandler<SearchAuditEventsQuery, AuditEventPage>
    {
        public async Task<Result<AuditEventPage>> Handle(SearchAuditEventsQuery request, CancellationToken cancellationToken)
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
            if (request.CustomerId is { } customerId)
                query = query.Where(e => e.CustomerId == customerId);
            if (request.TransactionId is { } transactionId)
                query = query.Where(e => e.TransactionId == transactionId);
            if (!string.IsNullOrWhiteSpace(request.ExternalTransactionId))
                query = query.Where(e => e.ExternalTransactionId == request.ExternalTransactionId);
            if (request.From is { } from)
                query = query.Where(e => e.OccurredAt >= from);
            if (request.To is { } to)
                query = query.Where(e => e.OccurredAt <= to);

            var totalCount = await query.CountAsync(cancellationToken);

            var events = await query
                .OrderByDescending(e => e.OccurredAt)
                .ThenBy(e => e.Id)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            return Result.Success(new AuditEventPage(
                events.Select(AuditEventDto.From).ToList(), request.PageNumber, request.PageSize, totalCount));
        }
    }
}