namespace TransactionAggregationAPI.DTOs.Customer
{
    public sealed record UpdateCustomerRequest(
        string Email,
        string Name);
}
