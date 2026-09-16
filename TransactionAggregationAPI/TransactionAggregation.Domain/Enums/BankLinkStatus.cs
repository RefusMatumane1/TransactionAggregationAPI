namespace TransactionAggregation.Domain.Enums
{
    public enum BankLinkStatus
    {
        /// <summary>Authorization URL was issued but the customer hasn't completed consent yet.</summary>
        PendingAuthorization = 0,
        Active = 1,
        /// <summary>Access/refresh token is expired or revoked by the bank; customer must re-consent.</summary>
        NeedsReauthorization = 2,
        Revoked = 3
    }
}
