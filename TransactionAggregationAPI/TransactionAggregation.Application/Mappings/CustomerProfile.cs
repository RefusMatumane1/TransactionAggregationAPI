using Mapster;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Mappings
{
    public class CustomerProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Customer, CustomerDto>
                .NewConfig()
                .Map(dest => dest.Id, src => src.Id.Value);

            //TypeAdapterConfig<CustomerDto, CustomerResponse>
            //    .NewConfig();

            //TypeAdapterConfig<CreateCustomerRequest, CreateCustomerCommand>
            //    .NewConfig();

            //TypeAdapterConfig<UpdateCustomerRequest, UpdateCustomerCommand>
            //    .NewConfig();
        }
    }
}
