using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Currency;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Errors;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Common.ValueObjects;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction
{
    internal sealed class GetTransactionQueryHandler(ITransactionsDbContext context, ICurrencyCatalog catalog)
        : IQueryHandler<GetTransactionQuery, TransactionDto>
    {
        public async Task<Result<TransactionDto>> Handle(GetTransactionQuery request, CancellationToken cancellationToken)
        {
            if (request.ConvertTo is not null && !catalog.IsSupported(request.ConvertTo))
                return Result.Failure<TransactionDto>(Error.Validation(
                    $"convertTo must be one of the supported currencies: {string.Join(", ", catalog.Supported)}."));

            var transactionId = TransactionId.CreateFrom(request.TransactionId);

            var transaction = await context.Transactions
                .Ledger(new TransactionFilter(request.Access))
                .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

            return transaction is null
                ? Result.Failure<TransactionDto>(TransactionErrors.NotFound(request.TransactionId))
                : Result.Success(TransactionDto.From(transaction).Converted(catalog, request.ConvertTo));
        }
    }
}