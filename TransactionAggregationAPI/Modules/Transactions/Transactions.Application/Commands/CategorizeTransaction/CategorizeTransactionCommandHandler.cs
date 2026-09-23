using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstractions;
using SharedKernel.Common.Interfaces;
using Modules.Transactions.Application.Common.Interfaces;
using SharedKernel.Common.Models;
using Modules.Transactions.Application.Common.Models;
using Modules.Transactions.Domain.Common.ValueObjects;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Commands.CategorizeTransaction
{
    internal sealed class CategorizeTransactionCommandHandler(ITransactionsDbContext _context)
        : ICommandHandler<CategorizeTransactionCommand>
    {
        public async Task<Result> Handle(CategorizeTransactionCommand request, CancellationToken cancellationToken)
        {
            var transactionId = TransactionId.CreateFrom(request.TransactionId);

            var transaction = await _context.Transactions.FirstOrDefaultAsync(
                t => t.Id == transactionId, cancellationToken);

            if (transaction is null)
                return Result.Failure(Error.NotFound("Transaction", request.TransactionId));

            transaction.Categorize(request.Category);

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}