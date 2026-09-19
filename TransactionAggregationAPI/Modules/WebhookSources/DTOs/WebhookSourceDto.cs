namespace Modules.WebhookSources.DTOs
{
    public sealed record WebhookSourceDto(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastUsedAt);
}
