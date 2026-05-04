using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Commands.Customer.Login;

internal sealed class LoginUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider) : ICommandHandler<LoginUserCommand, string>
{
    public async Task<Result<string>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        Domain.Entities.Customer? user = await context.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        if (user is null)
        {
            return Result.Failure<string>(Error.NotFound("Customer", command.Email));
        }

        bool verified = passwordHasher.Verify(command.Password, user.PasswordHash);

        if (!verified)
        {
            return Result.Failure<string>(Error.Failure(
                "Customer.InvalidCredentials",
                "Invalid email or password."));
        }

        string token = tokenProvider.Create(user);

        return token;
    }
}
