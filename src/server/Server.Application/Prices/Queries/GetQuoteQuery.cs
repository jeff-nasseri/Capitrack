using Server.Application.Common.Exceptions;

namespace Server.Application.Prices.Queries;

/// <summary>Returns the latest quote for a symbol.</summary>
/// <param name="Symbol">The symbol to quote.</param>
public record GetQuoteQuery(string Symbol) : IRequest<QuoteDto>;

/// <summary>Handles <see cref="GetQuoteQuery"/>.</summary>
public sealed class GetQuoteQueryHandler(
    IPriceService prices,
    ILogger<GetQuoteQueryHandler> logger)
    : IRequestHandler<GetQuoteQuery, QuoteDto>
{
    /// <summary>Resolves the quote for the symbol, throwing when none is available.</summary>
    public async Task<QuoteDto> Handle(GetQuoteQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetQuoteQuery));

        var q = await prices.GetQuoteAsync(request.Symbol.ToUpperInvariant());
        return q ?? throw new NotFoundException("Quote not found");
    }
}
