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

    // What staff may see about a bank: no ids for admin actions, no key material.
    public sealed record BankDto(string Code, string DisplayName, string Color, bool IsActive, DateTime? LastDeliveryAt);
}