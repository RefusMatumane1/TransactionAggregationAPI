namespace Modules.BankLinks.Infrastructure.Providers
{
    public sealed class BankAggregatorUnauthorizedException : Exception
    {
        public BankAggregatorUnauthorizedException(string message) : base(message) { }
    }
}