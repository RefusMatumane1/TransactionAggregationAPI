
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Errors;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Models;
using Modules.Transactions.Domain.Common.ValueObjects;
using SharedKernel.Abstractions;
using SharedKernel.Common.Interfaces;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction
{
    internal sealed class GetTransactionQueryHandler(ITransactionsDbContext context,
        ILogger<GetTransactionQueryHandler> logger)
        : IQueryHandler<GetTransactionQuery, TransactionDto>
    {
        public async Task<Result<TransactionDto>> Handle(GetTransactionQuery request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetTransactionQuery for TransactionId: {TransactionId}", request.TransactionId);

            var transactionId = TransactionId.CreateFrom(request.TransactionId);
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var transaction = await context.Transactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == transactionId && t.CustomerId == customerId, cancellationToken);

            if (transaction is null)
                return Result.Failure<TransactionDto>(TransactionErrors.NotFound(request.TransactionId));

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