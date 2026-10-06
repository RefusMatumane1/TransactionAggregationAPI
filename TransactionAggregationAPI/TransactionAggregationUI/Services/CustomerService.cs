using TransactionAggregationUI.Models.Customers;
using TransactionAggregationUI.Models.Shared;

namespace TransactionAggregationUI.Services
{
    public class CustomerService(ApiClient api)
    {
        private const string BasePath = "api/v1/customers";
        public const int PageSize = 25;

        public Task<(CursorPage<CustomerSummaryModel>? Value, string? Error)> ListAsync(string? search, string? cursor) =>
            api.GetAsync<CursorPage<CustomerSummaryModel>>($"{BasePath}?{ApiQuery.Of(("search", search), ("cursor", cursor), ("pageSize", PageSize))}");

        public Task<(CustomerModel? Value, string? Error)> GetAsync(Guid id) =>
            api.GetAsync<CustomerModel>($"{BasePath}/{id}",
                notFoundMessage: "This customer doesn't exist, or none of their accounts are at your banks.");

        public Task<(CustomerModel? Value, string? Error)> CreateAsync(string reference, string name) =>
            api.SendAsync<CustomerModel>(HttpMethod.Post, BasePath, new { Reference = reference, Name = name });

        // Linking an account that is already linked succeeds and changes nothing.
        public Task<(CustomerModel? Value, string? Error)> LinkAsync(Guid id, string institution, string externalAccountId) =>
            api.SendAsync<CustomerModel>(HttpMethod.Post, $"{BasePath}/{id}/accounts",
                new { Institution = institution, ExternalAccountId = externalAccountId });

        public Task<string?> UnlinkAsync(Guid id, string institution, string externalAccountId) =>
            api.SendAsync(HttpMethod.Delete,
                $"{BasePath}/{id}/accounts?{ApiQuery.Of(("institution", institution), ("externalAccountId", externalAccountId))}");
    }
}