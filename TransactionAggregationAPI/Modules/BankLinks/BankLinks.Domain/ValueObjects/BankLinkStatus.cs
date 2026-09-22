namespace Modules.BankLinks.Domain.ValueObjects
{
    public enum BankLinkStatus
    {
        PendingAuthorization = 0,
        Active = 1,
        NeedsReauthorization = 2,
        Revoked = 3
    }
}
