using TransactionAggregation.Domain.Enums;

namespace TransactionAggregationAPI.DTOs.BankLink
{
    public sealed record InitiateBankLinkRequest(Institution Institution);
}