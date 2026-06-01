using Server.Application.Common.Exceptions;

namespace Server.Application.Prices.Queries;

public record GetQuoteQuery(string Symbol) : IRequest<QuoteDto>;

public sealed class GetQuoteQueryHandler(IPriceService prices) : IRequestHandler<GetQuoteQuery, QuoteDto>
{
    public async Task<QuoteDto> Handle(GetQuoteQuery request, CancellationToken cancellationToken)
    {
        var q = await prices.GetQuoteAsync(request.Symbol.ToUpperInvariant());
        return q ?? throw new NotFoundException("Quote not found");
    }
}
