using Server.Application.Common.Exceptions;
using ValidationException = Server.Application.Common.Exceptions.ValidationException;

namespace Server.Application.Prices.Queries;

public record GetDailyWealthQuery(string? Start, string? End) : IRequest<List<DailyWealthDto>>;

public sealed class GetDailyWealthQueryHandler(IWealthService wealth)
    : IRequestHandler<GetDailyWealthQuery, List<DailyWealthDto>>
{
    public async Task<List<DailyWealthDto>> Handle(GetDailyWealthQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Start) || string.IsNullOrEmpty(request.End))
            throw new ValidationException("start and end dates required (YYYY-MM-DD)");

        return await wealth.GetDailyWealthAsync(request.Start!, request.End!);
    }
}
