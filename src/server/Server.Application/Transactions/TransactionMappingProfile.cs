using Server.Domain.Transactions;

namespace Server.Application.Transactions;

/// <summary>AutoMapper profile mapping <see cref="Transaction"/> aggregates to <see cref="TransactionDto"/>.</summary>
public sealed class TransactionMappingProfile : Profile
{
    /// <summary>Configures the transaction mappings.</summary>
    public TransactionMappingProfile()
    {
        CreateMap<Transaction, TransactionDto>()
            .ForCtorParam("Symbol", o => o.MapFrom(s => s.Symbol.Value))
            .ForCtorParam("Type", o => o.MapFrom(s => s.Type.Value))
            .ForCtorParam("Quantity", o => o.MapFrom(s => s.Quantity.Value))
            .ForCtorParam("Currency", o => o.MapFrom(s => s.Currency.Value))
            .ForCtorParam("Date", o => o.MapFrom(s => s.Date.Value))
            .ForCtorParam("AccountName", o => o.MapFrom(_ => (string?)null))
            .ForCtorParam("Tags", o => o.MapFrom(_ => new List<TagDto>()));
    }
}
