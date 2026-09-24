using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    /// <summary>
    /// The wire format of an inbound bank-transactions delivery, identical on every channel:
    /// it is both the REST webhook's JSON body (Presentation) and the value of records on the
    /// bank-transactions Kafka topic (Infrastructure). It lives here, beside the command both
    /// channels translate it into, so neither adapter depends on the other and the two
    /// shapes cannot drift apart.
    /// </summary>
    /// <param name="SchemaVersion">
    /// Version of this wire contract (docs/event-contracts.md). Omitted means 1, so every
    /// sender written before the field existed keeps working. Evolution is additive within a
    /// version; a breaking change ships as a new version accepted alongside the old one.
    /// </param>
    public sealed record BankTransactionsMessage(
        string ExternalAccountId,
        IReadOnlyList<BankTransactionMessageItem> Transactions,
        int? SchemaVersion = null)
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>Every version the ingestion pipeline can still read.</summary>
        public static readonly IReadOnlySet<int> SupportedSchemaVersions = new HashSet<int> { 1 };

        public int EffectiveSchemaVersion => SchemaVersion ?? CurrentSchemaVersion;

        // Transactions can be null when a body omits the array — validation reports that
        // as "must not be empty" instead of this throwing.
        public IReadOnlyList<ExternalTransactionDTO> ToExternalTransactionDtos() =>
            (Transactions ?? [])
                .Select(t => new ExternalTransactionDTO
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    Currency = t.Currency,
                    Description = t.Description,
                    Category = t.Category ?? string.Empty,
                    Date = t.Date,
                    Status = t.Status
                })
                .ToList();
    }

    /// <param name="Status">"pending" or "posted"; omitted means posted. A pending transaction is
    /// settled when the same Id arrives again as posted.</param>
    public sealed record BankTransactionMessageItem(
        string Id,
        decimal Amount,
        string Currency,
        string Description,
        string? Category,
        DateTime Date,
        string? Status = null);
}