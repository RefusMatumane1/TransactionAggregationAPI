
using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Commands.Customer.Login;

public sealed record LoginUserCommand(string Email, string Password) : ICommand<string>;
