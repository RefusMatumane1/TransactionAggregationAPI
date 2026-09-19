using MediatR;
using SharedKernel.Common.Models;

namespace SharedKernel.Abstractions
{
    public interface ICommandBase { }

    public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandBase { }
    public interface ICommand : IRequest<Result>, ICommandBase { }
}
