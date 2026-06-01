using System.Globalization;
using Server.Application.Common.Exceptions;

namespace Server.Application.Currencies.Queries;

public record ConversionResultDto(double Result, double Rate);

public record ConvertCurrencyQuery(string? From, string? To, string? Amount) : IRequest<ConversionResultDto>;

public sealed class ConvertCurrencyValidator : AbstractValidator<ConvertCurrencyQuery>
{
    public ConvertCurrencyValidator()
    {
        RuleFor(x => x.From).NotEmpty().WithMessage("from, to, and amount required");
        RuleFor(x => x.To).NotEmpty().WithMessage("from, to, and amount required");
        RuleFor(x => x.Amount).NotEmpty().WithMessage("from, to, and amount required");
    }
}

public sealed class ConvertCurrencyQueryHandler(ICurrencyRateRepository rates)
    : IRequestHandler<ConvertCurrencyQuery, ConversionResultDto>
{
    public async Task<ConversionResultDto> Handle(ConvertCurrencyQuery request, CancellationToken cancellationToken)
    {
        var amount = double.TryParse(request.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var a) ? a : 0;

        var from = CurrencyCode.Create(request.From);
        var to = CurrencyCode.Create(request.To);

        if (from.Value == to.Value)
            return new ConversionResultDto(amount, 1.0);

        var rate = await rates.GetPairAsync(from.Value, to.Value, cancellationToken)
                   ?? throw new NotFoundException($"No rate found for {request.From} to {request.To}");

        return new ConversionResultDto(amount * rate.Rate, rate.Rate);
    }
}
