using Server.Domain.Currencies;

namespace Server.Application.Currencies.Commands;

/// <summary>Inserts or updates a currency-conversion rate for a pair.</summary>
/// <param name="FromCurrency">The source currency code (required).</param>
/// <param name="ToCurrency">The target currency code (required).</param>
/// <param name="Rate">The conversion rate (required).</param>
public record UpsertCurrencyRateCommand(string? FromCurrency, string? ToCurrency, double? Rate)
    : IRequest<CurrencyRateDto>;

/// <summary>Validates <see cref="UpsertCurrencyRateCommand"/>.</summary>
public sealed class UpsertCurrencyRateValidator : AbstractValidator<UpsertCurrencyRateCommand>
{
    /// <summary>Configures the validation rules.</summary>
    public UpsertCurrencyRateValidator()
    {
        RuleFor(x => x.FromCurrency).NotEmpty().WithMessage("from_currency, to_currency, and rate required");
        RuleFor(x => x.ToCurrency).NotEmpty().WithMessage("from_currency, to_currency, and rate required");
        RuleFor(x => x.Rate).NotNull().WithMessage("from_currency, to_currency, and rate required");
    }
}

/// <summary>Handles <see cref="UpsertCurrencyRateCommand"/>.</summary>
public sealed class UpsertCurrencyRateHandler(
    ICurrencyRateRepository rates,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<UpsertCurrencyRateHandler> logger)
    : IRequestHandler<UpsertCurrencyRateCommand, CurrencyRateDto>
{
    /// <summary>Upserts the rate for the currency pair, persists it and returns the resulting DTO.</summary>
    public async Task<CurrencyRateDto> Handle(UpsertCurrencyRateCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(UpsertCurrencyRateCommand));

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
        return mapper.Map<CurrencyRateDto>(existing);
    }
}
