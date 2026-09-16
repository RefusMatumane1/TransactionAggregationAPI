using MediatR;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Abstractions
{
    public interface ICommandBase { }

    public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandBase { }
    public interface ICommand : IRequest<Result>, ICommandBase { }
}