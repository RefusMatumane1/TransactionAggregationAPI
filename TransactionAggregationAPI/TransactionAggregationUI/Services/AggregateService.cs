using TransactionAggregationUI.Models.Aggregates;

namespace TransactionAggregationUI.Services
{
    public class AggregateService(ApiClient api)
    {
        private static string BasePath(ReportFilter f) =>
            f.CustomerId is { } customer ? $"api/v1/customers/{customer}/aggregates" : "api/v1/transactions/aggregates";
        private const string DateFormat = "yyyy-MM-dd";
        private const int MonthGranularity = 2;

        public Task<(InstitutionBreakdownModel? Value, string? Error)> GetInstitutionsAsync(ReportFilter filter) =>
            api.GetAsync<InstitutionBreakdownModel>($"{BasePath(filter)}/institutions?{Query(filter, includeAccount: false)}");

        public Task<(CategoryBreakdownModel? Value, string? Error)> GetCategoriesAsync(ReportFilter filter, bool income = false) =>
            api.GetAsync<CategoryBreakdownModel>($"{BasePath(filter)}/categories?{Query(filter)}{(income ? "&direction=Income" : "")}");

        public Task<(CashFlowSeriesModel? Value, string? Error)> GetMonthlyCashFlowAsync(ReportFilter filter) =>
            api.GetAsync<CashFlowSeriesModel>($"{BasePath(filter)}/cash-flow?{Query(filter)}&granularity={MonthGranularity}");

        public Task<(PeriodComparisonModel? Value, string? Error)> GetComparisonAsync(ReportFilter filter) =>
            api.GetAsync<PeriodComparisonModel>($"{BasePath(filter)}/comparison?{Query(filter)}");

        private static string Query(ReportFilter f, bool includeAccount = true) => ApiQuery.Of(
            ("from", f.From.ToString(DateFormat)),
            ("to", f.To.ToString(DateFormat)),
            ("institution", f.Institution),
            ("externalAccountId", includeAccount ? f.ExternalAccountId : null));
    }
}