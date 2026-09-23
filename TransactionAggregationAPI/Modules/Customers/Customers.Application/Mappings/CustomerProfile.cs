using Mapster;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Domain;

namespace Modules.Customers.Application.Mappings
{
    public class CustomerProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Customer, CustomerDto>
                .NewConfig()
                .Map(dest => dest.Id, src => src.Id.Value);

        }
    }
}
