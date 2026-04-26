using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Abstractions
{
    public interface ICommandHandler<in TCommand>
        : IRequestHandler<TCommand, Result>
        where TCommand : ICommand
    {
    }

    public interface ICommandHandler<in TCommand, TResponse>
        : IRequestHandler<TCommand, Result<TResponse>>
        where TCommand : ICommand<TResponse>
    {
    }
}
