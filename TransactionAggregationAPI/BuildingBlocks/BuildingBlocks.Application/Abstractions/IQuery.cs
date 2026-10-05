using MediatR;
using SharedKernel.Common.Models;

namespace BuildingBlocks.Application.Abstractions
{
    public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }
}