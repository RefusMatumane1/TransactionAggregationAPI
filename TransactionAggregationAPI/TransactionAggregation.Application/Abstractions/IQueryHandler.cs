using MediatR;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Abstractions
{
    public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
        where TQuery : IQuery<TResponse>
    {
    }
}
