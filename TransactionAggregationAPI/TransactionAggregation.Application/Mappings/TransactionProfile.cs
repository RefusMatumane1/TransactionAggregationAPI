using Mapster;
using TransactionAggregation.Application.Features.Transactions.DTOs;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Mappings
{
    public class TransactionProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Transaction, TransactionDto>()
                .Map(dest => dest.Id, src => src.Id.Value)
                .Map(dest => dest.CustomerId, src => src.CustomerId.Value)
                .Map(dest => dest.AccountId, src => src.AccountId != null ? (Guid?)src.AccountId.Value : null)
                .Map(dest => dest.Amount, src => src.Amount.Amount)
                .Map(dest => dest.Currency, src => src.Amount.Currency)
                .Map(dest => dest.Description, src => src.Description)
                .Map(dest => dest.Category, src => src.Category)
                .Map(dest => dest.Status, src => src.Status)
                .Map(dest => dest.Source, src => src.Source.Name)
                .Map(dest => dest.Date, src => src.Date)
                .Map(dest => dest.CreatedAt, src => src.CreatedAt)
                .Map(dest => dest.Metadata, src => src.Metadata)
                .AfterMapping((src, dest) =>
                {
                    // Additional post-mapping logic if needed
                });
        }
    }
}
