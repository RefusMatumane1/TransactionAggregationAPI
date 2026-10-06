namespace BuildingBlocks.Application.Abstractions.Authentication
{
    public interface IUserContext
    {
        Guid UserId { get; }

        InstitutionAccess InstitutionAccess { get; }
    }
}