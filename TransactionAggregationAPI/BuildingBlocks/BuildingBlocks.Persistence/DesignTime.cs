namespace BuildingBlocks.Persistence
{
    public static class DesignTime
    {
        public const string ConnectionStringVariable = "ConnectionStrings__transactiondb";

        public static string ConnectionString =>
            Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException(
                    $"Set the {ConnectionStringVariable} environment variable to run EF Core tooling, e.g. " +
                    $"{ConnectionStringVariable}=\"Host=localhost;Database=transactiondb;Username=<user>;Password=<password>\". " +
                    "`dotnet ef migrations add` never connects, so any syntactically valid value works for it.");

    }
}