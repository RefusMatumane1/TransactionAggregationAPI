namespace Modules.WebhookSources.Application.DTOs
{
    public sealed record WebhookSourceDto(
        Guid Id,
        string Code,
        string DisplayName,
        string Color,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? LastUsedAt,
        bool SigningKeyRegistered);

    public sealed record BankDto(string Code, string DisplayName, string Color, bool IsActive, DateTime? LastDeliveryAt);
}