using Server.Application.Common.Exceptions;

namespace Server.Application.Currencies.Commands;

/// <summary>Updates an existing currency-conversion rate.</summary>
/// <param name="Id">The rate's identifier.</param>
/// <param name="FromCurrency">The new source currency, or null/empty to keep the current value.</param>
/// <param name="ToCurrency">The new target currency, or null/empty to keep the current value.</param>
/// <param name="Rate">The new rate, or null to keep the current value.</param>
public record UpdateCurrencyRateCommand(int Id, string? FromCurrency, string? ToCurrency, double? Rate)
    : IRequest<CurrencyRateDto>;

/// <summary>Handles <see cref="UpdateCurrencyRateCommand"/>.</summary>
public sealed class UpdateCurrencyRateHandler(
    ICurrencyRateRepository rates,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<UpdateCurrencyRateHandler> logger)
    : IRequestHandler<UpdateCurrencyRateCommand, CurrencyRateDto>
{
    /// <summary>Loads, updates and persists the rate and returns the resulting DTO.</summary>
    public async Task<CurrencyRateDto> Handle(UpdateCurrencyRateCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(UpdateCurrencyRateCommand));

        var rate = await rates.GetAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Rate not found");

        var from = CurrencyCode.Create(
            string.IsNullOrEmpty(request.FromCurrency) ? rate.FromCurrency.Value : request.FromCurrency);
        var to = CurrencyCode.Create(
            string.IsNullOrEmpty(request.ToCurrency) ? rate.ToCurrency.Value : request.ToCurrency);
        var value = request.Rate ?? rate.Rate;

        rate.Update(from, to, value);
        await uow.SaveChangesAsync(cancellationToken);
        return mapper.Map<CurrencyRateDto>(rate);
    }
}
