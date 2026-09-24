using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Errors;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Common.ValueObjects;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Features.Transactions.Commands.CategorizeTransaction
{
    internal sealed class CategorizeTransactionCommandHandler(ITransactionsDbContext _context)
        : ICommandHandler<CategorizeTransactionCommand>
    {
        public async Task<Result> Handle(CategorizeTransactionCommand request, CancellationToken cancellationToken)
        {
            var transactionId = TransactionId.CreateFrom(request.TransactionId);
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var transaction = await _context.Transactions.FirstOrDefaultAsync(
                t => t.Id == transactionId && t.CustomerId == customerId, cancellationToken);

            if (transaction is null)
                return Result.Failure(TransactionErrors.NotFound(request.TransactionId));

            transaction.Categorize(request.Category);

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}