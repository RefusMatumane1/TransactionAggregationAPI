namespace Modules.Transactions.Application.Common.DTOs
{
    public sealed record ConversionDto(
        string Target,
        string RatesVersion,
        IReadOnlyList<ConversionRateDto> Rates,
        IReadOnlyList<string> UnconvertedCurrencies);

    public sealed record ConversionRateDto(string Currency, decimal Rate);
}