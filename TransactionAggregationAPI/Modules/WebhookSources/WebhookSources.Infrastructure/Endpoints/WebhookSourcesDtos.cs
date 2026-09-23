namespace Modules.WebhookSources.Infrastructure.Endpoints
{
    public sealed record CreateWebhookSourceRequest(string Name);

    public sealed record WebhookSourceResponse(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime? LastUsedAt);
    public sealed record CreateWebhookSourceResponse(Guid Id, string Name, string ApiKey);
    public sealed record RotateWebhookSourceKeyResponse(string ApiKey);
}
