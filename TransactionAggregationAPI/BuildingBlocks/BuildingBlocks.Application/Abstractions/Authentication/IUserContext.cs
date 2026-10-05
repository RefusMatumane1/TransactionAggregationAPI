namespace BuildingBlocks.Application.Abstractions.Authentication
{
    public interface IUserContext
    {
        // The identity provider's subject id of the signed-in user.
        Guid UserId { get; }

        InstitutionAccess InstitutionAccess { get; }
    }
}