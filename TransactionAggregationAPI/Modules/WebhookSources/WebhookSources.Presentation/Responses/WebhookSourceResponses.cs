using Modules.WebhookSources.Application.DTOs;
using Modules.WebhookSources.Application.Features.CreateWebhookSource;

namespace Modules.WebhookSources.Presentation.Responses
{
    public sealed record WebhookSourceResponse(
        Guid Id, string Code, string DisplayName, string Color, bool IsActive, DateTime CreatedAt, DateTime? LastUsedAt,
        bool SigningKeyRegistered)
    {
        internal static WebhookSourceResponse From(WebhookSourceDto source) =>
            new(source.Id, source.Code, source.DisplayName, source.Color, source.IsActive, source.CreatedAt, source.LastUsedAt,
                source.SigningKeyRegistered);
    }

    public sealed record CreateWebhookSourceResponse(Guid Id, string Code, string DisplayName, string Color, string ApiKey)
    {
        internal static CreateWebhookSourceResponse From(CreateWebhookSourceResult result) =>
            new(result.Id, result.Code, result.DisplayName, result.Color, result.ApiKey);
    }

    public sealed record RotateWebhookSourceKeyResponse(string ApiKey);

    public sealed record BankResponse(string Code, string DisplayName, string Color, bool IsActive, DateTime? LastDeliveryAt)
    {
        internal static BankResponse From(BankDto bank) =>
            new(bank.Code, bank.DisplayName, bank.Color, bank.IsActive, bank.LastDeliveryAt);
    }
}