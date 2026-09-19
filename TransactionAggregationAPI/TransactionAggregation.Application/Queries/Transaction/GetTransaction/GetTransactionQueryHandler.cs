
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using SharedKernel.Common.Models;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Queries.Transaction.GetTransaction
{
    internal sealed class GetTransactionQueryHandler(IApplicationDbContext context,
        ILogger<GetTransactionQueryHandler> logger)
        : IQueryHandler<GetTransactionQuery, TransactionDto>
    {
        public async Task<Result<TransactionDto>> Handle(GetTransactionQuery request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetTransactionQuery for TransactionId: {TransactionId}", request.TransactionId);

            var transactionId = TransactionId.CreateFrom(request.TransactionId);

            var transaction = await context.Transactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

            if (transaction is null)
                return Result.Failure<TransactionDto>(Error.NotFound("Transaction", request.TransactionId));

            var dto = new TransactionDto(
                transaction.Id.Value,
                transaction.CustomerId.Value,
                transaction.Amount.Amount,
                transaction.Amount.Currency,
                transaction.Date,
                transaction.Description,
                transaction.Category,
                transaction.Status,
                transaction.Source.Name,
                transaction.AccountId != null ? transaction.AccountId.Value : null);

            return Result.Success(dto);
        }
    }
}