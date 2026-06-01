using ValidationException = Server.Application.Common.Exceptions.ValidationException;

namespace Server.Application.Prices.Queries;

/// <summary>Returns the latest quotes for a set of symbols.</summary>
/// <param name="Symbols">The symbols to quote (required, non-empty).</param>
public record GetQuotesQuery(List<string>? Symbols) : IRequest<Dictionary<string, QuoteDto?>>;

/// <summary>Handles <see cref="GetQuotesQuery"/>.</summary>
public sealed class GetQuotesQueryHandler(
    IPriceService prices,
    ILogger<GetQuotesQueryHandler> logger)
    : IRequestHandler<GetQuotesQuery, Dictionary<string, QuoteDto?>>
{
    /// <summary>Resolves a quote for each requested symbol.</summary>
    public async Task<Dictionary<string, QuoteDto?>> Handle(GetQuotesQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetQuotesQuery));

        if (request.Symbols is null || request.Symbols.Count == 0)
            throw new ValidationException("symbols is required");

        var results = new Dictionary<string, QuoteDto?>();
        foreach (var sym in request.Symbols)
        {
            var s = sym.ToUpperInvariant();
            results[s] = await prices.GetQuoteAsync(s);
        }
        return results;
    }
}
