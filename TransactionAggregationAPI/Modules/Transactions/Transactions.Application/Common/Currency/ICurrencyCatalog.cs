namespace Modules.Transactions.Application.Common.Currency
{
    public interface ICurrencyCatalog
    {
        string BaseCurrency { get; }

        IReadOnlyList<string> Supported { get; }

        string Version { get; }

        bool IsSupported(string? currency);

        decimal? Rate(string from, string to);

        decimal? Convert(decimal amount, string from, string to);
    }
}