using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Application.Commands.CreateTransaction
{
    internal sealed class CreateTransactionCommandHandler(
        IApplicationDbContext _context,
        ITransactionCategorizationService _categorizationService) 
        : ICommandHandler<CreateTransactionCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var customerId = CustomerId.CreateFrom(request.CustomerId);
                var amount = Money.Create(request.Amount, request.Currency);
                var accountId = request.AccountId.HasValue
                    ? AccountId.CreateFrom(request.AccountId.Value)
                    : null;

                var transaction = Transaction.Create(
                    customerId,
                    amount,
                    request.Description,
                    Domain.Enums.TransactionCategory.Uncategorized,
                    TransactionSource.Create(request.SourceSystem, Guid.NewGuid().ToString()),
                    accountId);

                // Auto-categorize based on description and amount
                var category = await _categorizationService.CategorizeTransactionAsync(
                    transaction, cancellationToken);
                transaction.Categorize(category);

                await _context.Transactions.AddAsync(transaction, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                return Result.Success(transaction.Id.Value);
            }
            catch (DomainException ex)
            {
                return Result.Failure<Guid>(Error.Validation(ex.Message));
            }
        }
    }
}
