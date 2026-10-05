using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Common.Errors
{
    public static class TransactionErrors
    {
        public static Error NotFound(Guid transactionId) => Error.NotFound("Transaction", transactionId);

        public static Error SourceNotAuthorizedForInstitution(string sourceName, string institution) =>
            Error.NotPermitted(
                "Ingestion.SourceNotAuthorized",
                $"Bank '{sourceName}' can only deliver its own transactions, not institution '{institution}'.");
    }
}