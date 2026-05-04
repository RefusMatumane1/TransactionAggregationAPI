using MediatR;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Abstractions
{
    /// <summary>Marker interface shared by all commands, regardless of return type.</summary>
    public interface ICommandBase { }

    public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandBase { }
    public interface ICommand : IRequest<Result>, ICommandBase { }
}
