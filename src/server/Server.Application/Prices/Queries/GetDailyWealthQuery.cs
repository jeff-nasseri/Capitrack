using ValidationException = Server.Application.Common.Exceptions.ValidationException;

namespace Server.Application.Prices.Queries;

/// <summary>Returns stored daily wealth snapshots between two dates.</summary>
/// <param name="Start">The inclusive start date (required, yyyy-MM-dd).</param>
/// <param name="End">The inclusive end date (required, yyyy-MM-dd).</param>
public record GetDailyWealthQuery(string? Start, string? End) : IRequest<List<DailyWealthDto>>;

/// <summary>Handles <see cref="GetDailyWealthQuery"/>.</summary>
public sealed class GetDailyWealthQueryHandler(
    IWealthService wealth,
    ILogger<GetDailyWealthQueryHandler> logger)
    : IRequestHandler<GetDailyWealthQuery, List<DailyWealthDto>>
{
    /// <summary>Validates the date range and delegates to the wealth service.</summary>
    public async Task<List<DailyWealthDto>> Handle(GetDailyWealthQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetDailyWealthQuery));

        if (string.IsNullOrEmpty(request.Start) || string.IsNullOrEmpty(request.End))
            throw new ValidationException("start and end dates required (YYYY-MM-DD)");

        return await wealth.GetDailyWealthAsync(request.Start!, request.End!);
    }
}
