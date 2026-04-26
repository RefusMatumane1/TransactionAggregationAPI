namespace TransactionAggregationAPI.DTOs.Customer
{
    public sealed record CustomerResponse(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt = null);
}
