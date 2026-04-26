using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Application.Commands.CreateTransaction
{
    internal sealed class CreateTransactionCommandHandler : ICommandHandler<CreateTransactionCommand, Guid>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITransactionCategorizationService _categorizationService;

        public CreateTransactionCommandHandler(
            IApplicationDbContext context,
            ITransactionCategorizationService categorizationService)
        {
            _context = context;
            _categorizationService = categorizationService;
        }

        public async Task<Result<Guid>> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var customerId = CustomerId.CreateFrom(request.CustomerId);
                var amount = Money.Create(request.Amount, request.Currency);

                var transaction = Transaction.Create(
                    customerId,
                    amount,
                    request.Description,
                    Domain.Enums.TransactionCategory.Uncategorized,
                    TransactionSource.Create(request.SourceSystem, ""));

                // Auto-categorize based on description and amount
                var category = await _categorizationService.CategorizeTransactionAsync(
                    transaction, cancellationToken);
                transaction.Categorize(category);

                await _context.Transactions.AddAsync(transaction, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                return Result<Guid>.Success(transaction.Id.Value);
            }
            catch (DomainException ex)
            {
                return Result.Failure<Guid>(Error.Validation(ex.Message));
            }
        }
    }
}
