using System.Globalization;
using Server.Application.Common.Exceptions;

namespace Server.Application.Currencies.Queries;

/// <summary>The result of a currency conversion.</summary>
/// <param name="Result">The converted amount.</param>
/// <param name="Rate">The conversion rate applied.</param>
public record ConversionResultDto(double Result, double Rate);

/// <summary>Converts an amount between two currencies using stored rates.</summary>
/// <param name="From">The source currency code (required).</param>
/// <param name="To">The target currency code (required).</param>
/// <param name="Amount">The amount to convert (required).</param>
public record ConvertCurrencyQuery(string? From, string? To, string? Amount) : IRequest<ConversionResultDto>;

/// <summary>Validates <see cref="ConvertCurrencyQuery"/>.</summary>
public sealed class ConvertCurrencyValidator : AbstractValidator<ConvertCurrencyQuery>
{
    /// <summary>Configures the validation rules.</summary>
    public ConvertCurrencyValidator()
    {
        RuleFor(x => x.From).NotEmpty().WithMessage("from, to, and amount required");
        RuleFor(x => x.To).NotEmpty().WithMessage("from, to, and amount required");
        RuleFor(x => x.Amount).NotEmpty().WithMessage("from, to, and amount required");
    }
}

/// <summary>Handles <see cref="ConvertCurrencyQuery"/>.</summary>
public sealed class ConvertCurrencyQueryHandler(
    ICurrencyRateRepository rates,
    ILogger<ConvertCurrencyQueryHandler> logger)
    : IRequestHandler<ConvertCurrencyQuery, ConversionResultDto>
{
    /// <summary>Resolves the applicable rate and returns the converted amount.</summary>
    public async Task<ConversionResultDto> Handle(ConvertCurrencyQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ConvertCurrencyQuery));

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
