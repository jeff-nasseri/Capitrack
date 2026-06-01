using Server.Application.Common.Exceptions;
using ValidationException = Server.Application.Common.Exceptions.ValidationException;

namespace Server.Application.Prices.Queries;

public record GetQuotesQuery(List<string>? Symbols) : IRequest<Dictionary<string, QuoteDto?>>;

public sealed class GetQuotesQueryHandler(IPriceService prices)
    : IRequestHandler<GetQuotesQuery, Dictionary<string, QuoteDto?>>
{
    public async Task<Dictionary<string, QuoteDto?>> Handle(GetQuotesQuery request, CancellationToken cancellationToken)
    {
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
