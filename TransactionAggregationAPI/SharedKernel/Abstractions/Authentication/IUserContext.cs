namespace SharedKernel.Abstractions.Authentication
{
    public interface IUserContext
    {
        Guid UserId { get; }
    }
}
