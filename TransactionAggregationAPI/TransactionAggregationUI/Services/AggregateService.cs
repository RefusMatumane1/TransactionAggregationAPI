using TransactionAggregationUI.Models.Aggregates;

namespace TransactionAggregationUI.Services
{
    // Every call returns the error alongside the value, so a failed report is shown as a failure
    // the user can retry, not as an empty one.
    public class AggregateService(ApiClient api)
    {
        private const string BasePath = "api/v1/transactions/aggregates";
        private const string DateFormat = "yyyy-MM-dd";
        private const int MonthGranularity = 2;

        public Task<(InstitutionBreakdownModel? Value, string? Error)> GetInstitutionsAsync(ReportFilter filter) =>
            api.GetAsync<InstitutionBreakdownModel>($"{BasePath}/institutions?{Query(filter, includeAccount: false)}");

        public Task<(CategoryBreakdownModel? Value, string? Error)> GetCategoriesAsync(ReportFilter filter, bool income = false) =>
            api.GetAsync<CategoryBreakdownModel>($"{BasePath}/categories?{Query(filter)}{(income ? "&direction=Income" : "")}");

        public Task<(CashFlowSeriesModel? Value, string? Error)> GetMonthlyCashFlowAsync(ReportFilter filter) =>
            api.GetAsync<CashFlowSeriesModel>($"{BasePath}/cash-flow?{Query(filter)}&granularity={MonthGranularity}");

        public Task<(PeriodComparisonModel? Value, string? Error)> GetComparisonAsync(ReportFilter filter) =>
            api.GetAsync<PeriodComparisonModel>($"{BasePath}/comparison?{Query(filter)}");

        private static string Query(ReportFilter f, bool includeAccount = true) => ApiQuery.Of(
            ("from", f.From.ToString(DateFormat)),
            ("to", f.To.ToString(DateFormat)),
            ("institution", f.Institution),
            ("externalAccountId", includeAccount ? f.ExternalAccountId : null));
    }
}