using MediatR;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;

namespace TransactionAggregation.Application.Common.Behaviors
{
    public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
     where TRequest : IRequest<TResponse>
     where TResponse : class
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

        public TransactionBehavior(
            IUnitOfWork unitOfWork,
            ILogger<TransactionBehavior<TRequest, TResponse>> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            // Only handle commands that modify data
            if (request is not ICommandBase)
                return await next();

            try
            {
                _logger.LogInformation("Begin transaction for request: {RequestName}", typeof(TRequest).Name);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                var response = await next();
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Transaction committed for request: {RequestName}", typeof(TRequest).Name);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transaction rolled back for request: {RequestName}", typeof(TRequest).Name);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }

}
