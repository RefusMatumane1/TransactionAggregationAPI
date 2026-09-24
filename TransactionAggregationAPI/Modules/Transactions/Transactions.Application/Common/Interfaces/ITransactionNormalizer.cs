using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Common.Interfaces
{
    /// <summary>
    /// The normalization stage of ingestion: turns one bank's raw transaction into the
    /// canonical <see cref="NormalizedTransaction"/>, using that institution's profile.
    /// Pure and deterministic — no I/O — so it runs inside the ingestion unit of work.
    /// </summary>
    public interface ITransactionNormalizer
    {
        NormalizedTransaction Normalize(ExternalTransactionDTO raw, string institution);
    }
}