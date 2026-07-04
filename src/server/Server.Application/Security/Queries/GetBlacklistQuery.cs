namespace Server.Application.Security.Queries;

/// <summary>Returns all active (non-expired) IP blacklist entries, newest first.</summary>
public record GetBlacklistQuery : IRequest<List<BlacklistedIpDto>>;

/// <summary>Handles <see cref="GetBlacklistQuery"/>.</summary>
public sealed class GetBlacklistQueryHandler(
    IBlacklistRepository blacklist,
    IMapper mapper,
    ILogger<GetBlacklistQueryHandler> logger)
    : IRequestHandler<GetBlacklistQuery, List<BlacklistedIpDto>>
{
    /// <summary>Loads the active blacklist and returns the resulting DTOs.</summary>
    public async Task<List<BlacklistedIpDto>> Handle(GetBlacklistQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetBlacklistQuery));
        var items = await blacklist.ListActiveAsync(DateTime.UtcNow, cancellationToken);
        return items.Select(b => mapper.Map<BlacklistedIpDto>(b)).ToList();
    }
}
