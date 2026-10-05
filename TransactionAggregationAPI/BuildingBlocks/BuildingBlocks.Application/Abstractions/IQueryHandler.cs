using MediatR;
using SharedKernel.Common.Models;

namespace BuildingBlocks.Application.Abstractions
{
    public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
        where TQuery : IQuery<TResponse>
    {
    }
}