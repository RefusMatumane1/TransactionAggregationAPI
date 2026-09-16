namespace TransactionAggregation.Domain.Enums
{
    public enum BankLinkStatus
    {
        PendingAuthorization = 0,
        Active = 1,
        NeedsReauthorization = 2,
        Revoked = 3
    }
}