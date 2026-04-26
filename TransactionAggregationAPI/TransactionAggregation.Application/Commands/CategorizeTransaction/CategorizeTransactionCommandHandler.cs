
using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Commands.CategorizeTransaction
{
    internal sealed class CategorizeTransactionCommandHandler(IApplicationDbContext _context)
        : ICommandHandler<CategorizeTransactionCommand>
    {

        public async Task<Result> Handle(CategorizeTransactionCommand request, CancellationToken cancellationToken)
        {
            var transactionId = TransactionId.CreateFrom(request.TransactionId);

            var transaction = await _context.Transactions.FirstOrDefaultAsync
                (t => t.Id == transactionId, cancellationToken);

            if (transaction is null)
                return Result.Failure(Error.NotFound("Transaction.NotFound", "Transaction not found"));

            transaction.Categorize(request.Category);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
