namespace TransactionAggregationUI.Models.WebhookSources;

public class WebhookSourceModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class CreateWebhookSourceResultModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class RotateWebhookSourceKeyResultModel
{
    public string ApiKey { get; set; } = string.Empty;
}
