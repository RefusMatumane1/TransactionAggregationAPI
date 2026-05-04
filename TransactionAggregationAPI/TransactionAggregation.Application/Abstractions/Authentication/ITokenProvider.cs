using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Abstractions.Authentication
{
    public interface ITokenProvider
    {
        string Create(Customer user);
    }
}
