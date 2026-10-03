using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace TransactionAggregationAPI.Authentication
{
    internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
    {
        private ClaimsPrincipal User =>
            httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("There is no HTTP request in this scope.");

        public Guid UserId =>
            Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
                ? userId
                : throw new InvalidOperationException("The request has no signed-in user with a valid 'sub' claim.");

        public InstitutionAccess InstitutionAccess =>
            User.IsInRole(Roles.Admin)
                ? InstitutionAccess.All
                : User.IsInRole(Roles.Staff)
                    ? InstitutionAccess.Only(User.FindAll(ClaimNames.Institutions).Select(c => c.Value))
                    : InstitutionAccess.None;
    }
}