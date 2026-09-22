using Modules.BankLinks.Domain.ValueObjects;

namespace TransactionAggregationAPI.DTOs.BankLink
{
    public sealed record InitiateBankLinkRequest(Institution Institution);
}