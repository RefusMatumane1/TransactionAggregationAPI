using Modules.Currency.Contracts;

namespace Modules.Currency.Presentation.Responses
{
    public sealed record CurrencyCatalogResponse(string BaseCurrency, string RatesVersion, IReadOnlyList<CurrencyResponse> Currencies)
    {
        internal static CurrencyCatalogResponse From(ICurrencyCatalog catalog) => new(
            catalog.BaseCurrency,
            catalog.Version,
            catalog.Supported.Select(code => new CurrencyResponse(code, catalog.Rate(code, catalog.BaseCurrency)!.Value)).ToList());
    }

    public sealed record CurrencyResponse(string Code, decimal RateToBase);
}