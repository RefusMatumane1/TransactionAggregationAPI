using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Errors;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Common.ValueObjects;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction
{
    internal sealed class GetTransactionQueryHandler(ITransactionsDbContext context)
        : IQueryHandler<GetTransactionQuery, TransactionDto>
    {
        // A transaction outside the caller's institutions is reported as not found, exactly like one
        // that does not exist, so ids cannot be probed for existence.
        public async Task<Result<TransactionDto>> Handle(GetTransactionQuery request, CancellationToken cancellationToken)
        {
            var transactionId = TransactionId.CreateFrom(request.TransactionId);

            var transaction = await context.Transactions
                .Ledger(new TransactionFilter(request.Access))
                .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

            return transaction is null
                ? Result.Failure<TransactionDto>(TransactionErrors.NotFound(request.TransactionId))
                : Result.Success(TransactionDto.From(transaction));
        }
    }
}