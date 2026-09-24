using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Common.Errors
{
    /// <summary>
    /// The single definition of "transaction not found". Handlers return it for a missing
    /// row, and endpoints return the same error for a transaction that exists but belongs to
    /// another customer — so the two responses are identical and can't be used to discover
    /// which transaction ids exist.
    /// </summary>
    public static class TransactionErrors
    {
        public static Error NotFound(Guid transactionId) => Error.NotFound("Transaction", transactionId);

        /// <summary>
        /// Permanent: retrying can't change which institutions a source is authorized for, so
        /// the inbox dead-letters it at once rather than spending its retry budget.
        /// </summary>
        public static Error SourceNotAuthorizedForAccount(string sourceName, string externalAccountId) =>
            Error.NotPermitted(
                "Ingestion.SourceNotAuthorized",
                $"Source '{sourceName}' is not authorized for the institution holding account '{externalAccountId}'.");
    }
}