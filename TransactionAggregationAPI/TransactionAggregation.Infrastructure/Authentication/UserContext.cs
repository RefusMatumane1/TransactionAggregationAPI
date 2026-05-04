using Microsoft.AspNetCore.Http;
using TransactionAggregation.Application.Abstractions.Authentication;

namespace TransactionAggregation.Infrastructure.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{

    public Guid UserId =>
        httpContextAccessor
            .HttpContext?
            .User
            .GetUserId() ??
        throw new ApplicationException("User context is unavailable");
}
