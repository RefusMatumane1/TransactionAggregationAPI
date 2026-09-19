using MediatR;
using SharedKernel.Common.Models;

namespace SharedKernel.Abstractions
{
    public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }
}
