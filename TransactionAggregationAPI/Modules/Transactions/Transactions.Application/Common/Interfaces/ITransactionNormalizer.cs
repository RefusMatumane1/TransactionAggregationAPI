using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionNormalizer
    {
        NormalizedTransaction Normalize(ExternalTransactionDTO raw, string institution);
    }
}