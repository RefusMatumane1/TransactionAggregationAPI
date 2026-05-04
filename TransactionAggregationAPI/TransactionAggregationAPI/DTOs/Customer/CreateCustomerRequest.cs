namespace TransactionAggregationAPI.DTOs.Customer
{
    public sealed record CreateCustomerRequest(
        string Email,
        string Name, string password);
}
