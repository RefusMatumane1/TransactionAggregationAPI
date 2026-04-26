using MediatR;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Abstractions
{
    public interface ICommand<TResponse> : IRequest<Result<TResponse>> { }
    public interface ICommand : IRequest<Result>{}
}
