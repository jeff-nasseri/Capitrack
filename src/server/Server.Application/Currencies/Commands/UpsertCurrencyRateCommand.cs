using Server.Application.Currencies;
using Server.Domain.Currencies;

namespace Server.Application.Currencies.Commands;

public record UpsertCurrencyRateCommand(string? FromCurrency, string? ToCurrency, double? Rate)
    : IRequest<CurrencyRateDto>;

public sealed class UpsertCurrencyRateValidator : AbstractValidator<UpsertCurrencyRateCommand>
{
    public UpsertCurrencyRateValidator()
    {
        RuleFor(x => x.FromCurrency).NotEmpty().WithMessage("from_currency, to_currency, and rate required");
        RuleFor(x => x.ToCurrency).NotEmpty().WithMessage("from_currency, to_currency, and rate required");
        RuleFor(x => x.Rate).NotNull().WithMessage("from_currency, to_currency, and rate required");
    }
}

public sealed class UpsertCurrencyRateHandler(ICurrencyRateRepository rates, IUnitOfWork uow)
    : IRequestHandler<UpsertCurrencyRateCommand, CurrencyRateDto>
{
    public async Task<CurrencyRateDto> Handle(UpsertCurrencyRateCommand request, CancellationToken cancellationToken)
    {
        var from = CurrencyCode.Create(request.FromCurrency);
        var to = CurrencyCode.Create(request.ToCurrency);

        var existing = await rates.GetPairAsync(from.Value, to.Value, cancellationToken);
        if (existing is not null)
        {
            existing.SetRate(request.Rate!.Value);
        }
        else
        {
            existing = CurrencyRate.Create(from, to, request.Rate!.Value);
            await rates.AddAsync(existing, cancellationToken);
        }

        await uow.SaveChangesAsync(cancellationToken);
        return existing.ToDto();
    }
}
