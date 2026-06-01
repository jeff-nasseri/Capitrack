using Server.Domain.Accounts;
using Server.Domain.Holdings;

namespace Server.Application.Accounts;

/// <summary>AutoMapper profile mapping account aggregates and holdings to their DTOs.</summary>
public sealed class AccountMappingProfile : Profile
{
    /// <summary>Configures the account and holding mappings.</summary>
    public AccountMappingProfile()
    {
        CreateMap<Account, AccountDto>()
            .ForCtorParam("Type", o => o.MapFrom(s => s.Type.Value))
            .ForCtorParam("Currency", o => o.MapFrom(s => s.Currency.Value))
            .ForCtorParam("Color", o => o.MapFrom(s => s.Color.Value))
            .ForCtorParam("Tags", o => o.MapFrom(_ => new List<TagDto>()));

        CreateMap<Holding, HoldingDto>()
            .ForCtorParam("Symbol", o => o.MapFrom(s => s.Symbol.Value));
    }
}
