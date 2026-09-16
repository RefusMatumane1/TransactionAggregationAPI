namespace TransactionAggregation.Application.Common.DTOs
{
    /// <summary>Admin-facing view of a WebhookSource — deliberately has no key/hash field at
    /// all, since the whole point of hashing is that nothing downstream of the database can
    /// ever see the key again.</summary>
    public sealed record WebhookSourceDto(
        Guid Id,
        string Name,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? LastUsedAt);
}
