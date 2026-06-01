using Server.Application.Common.Exceptions;
using Server.Application.Currencies;

namespace Server.Application.Currencies.Commands;

public record UpdateCurrencyRateCommand(int Id, string? FromCurrency, string? ToCurrency, double? Rate)
    : IRequest<CurrencyRateDto>;

public sealed class UpdateCurrencyRateHandler(ICurrencyRateRepository rates, IUnitOfWork uow)
    : IRequestHandler<UpdateCurrencyRateCommand, CurrencyRateDto>
{
    public async Task<CurrencyRateDto> Handle(UpdateCurrencyRateCommand request, CancellationToken cancellationToken)
    {
        var rate = await rates.GetAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Rate not found");

        var from = CurrencyCode.Create(
            string.IsNullOrEmpty(request.FromCurrency) ? rate.FromCurrency.Value : request.FromCurrency);
        var to = CurrencyCode.Create(
            string.IsNullOrEmpty(request.ToCurrency) ? rate.ToCurrency.Value : request.ToCurrency);
        var value = request.Rate ?? rate.Rate;

        rate.Update(from, to, value);
        await uow.SaveChangesAsync(cancellationToken);
        return rate.ToDto();
    }
}
