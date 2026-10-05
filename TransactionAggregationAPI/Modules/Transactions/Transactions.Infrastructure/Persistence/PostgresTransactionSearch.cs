using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Infrastructure.Persistence
{
    // ILIKE '%term%' on Description, served by IX_Transactions_Description_Trgm (pg_trgm GIN).
    internal sealed class PostgresTransactionSearch : ITransactionSearch
    {
        private const string EscapeCharacter = "\\";

        public IQueryable<Transaction> DescriptionContains(IQueryable<Transaction> transactions, string term)
        {
            var pattern = $"%{Escape(term)}%";
            return transactions.Where(t => EF.Functions.ILike(t.Description, pattern, EscapeCharacter));
        }

        internal static string Escape(string term) =>
            term.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter)
                .Replace("%", EscapeCharacter + "%")
                .Replace("_", EscapeCharacter + "_");
    }
}