namespace TransactionAggregation.Application.Abstractions.Authentication
{
    public sealed class KeycloakUserConflictException : Exception
    {
        public KeycloakUserConflictException(string message) : base(message) { }
    }
}