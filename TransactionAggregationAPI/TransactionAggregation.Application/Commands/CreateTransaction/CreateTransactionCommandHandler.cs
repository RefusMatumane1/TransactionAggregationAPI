using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Application.Commands.CreateTransaction
{
    internal sealed class CreateTransactionCommandHandler(
        IApplicationDbContext _context,
        ITransactionValidator _validator)
        : ICommandHandler<CreateTransactionCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var amount = Money.Create(request.Amount, request.Currency);
                var source = TransactionSource.Create(request.SourceSystem, Guid.NewGuid().ToString());

                var validation = await _validator.ValidateTransactionAsync(
                    amount, request.Description, source, request.TransactionDate, cancellationToken);

                if (!validation.IsValid)
                    return Result.Failure<Guid>(Error.Validation(
                        string.Join("; ", validation.Errors.Select(e => e.Message))));

                var customerId = CustomerId.CreateFrom(request.CustomerId);
                var accountId = request.AccountId.HasValue
                    ? AccountId.CreateFrom(request.AccountId.Value)
                    : null;

                var transaction = Transaction.Create(
                    customerId,
                    amount,
                    request.Description,
                    Domain.Enums.TransactionCategory.Uncategorized,
                    source,
                    accountId);

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
