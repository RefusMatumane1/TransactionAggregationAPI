using Modules.BankLinks.ValueObjects;

namespace TransactionAggregationAPI.DTOs.BankLink
{
    public sealed record InitiateBankLinkRequest(Institution Institution);
}