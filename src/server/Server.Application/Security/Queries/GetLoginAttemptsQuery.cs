namespace Server.Application.Security.Queries;

/// <summary>Returns a most-recent-first page of login attempts.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
public record GetLoginAttemptsQuery(int? Page, int? PageSize) : IRequest<List<LoginAttemptDto>>;

/// <summary>Returns the total number of recorded login attempts (for pagination headers).</summary>
public record GetLoginAttemptsCountQuery : IRequest<int>;

/// <summary>Handles <see cref="GetLoginAttemptsQuery"/>.</summary>
public sealed class GetLoginAttemptsQueryHandler(
    ILoginAttemptRepository attempts,
    IMapper mapper,
    ILogger<GetLoginAttemptsQueryHandler> logger)
    : IRequestHandler<GetLoginAttemptsQuery, List<LoginAttemptDto>>
{
    /// <summary>Loads the requested page of attempts and returns the resulting DTOs.</summary>
    public async Task<List<LoginAttemptDto>> Handle(GetLoginAttemptsQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetLoginAttemptsQuery));

        var pageSize = request.PageSize is > 0 and <= 200 ? request.PageSize.Value : 25;
        var page = request.Page is > 0 ? request.Page.Value : 1;
        var items = await attempts.ListAsync(pageSize, (page - 1) * pageSize, cancellationToken);
        return items.Select(a => mapper.Map<LoginAttemptDto>(a)).ToList();
    }
}

/// <summary>Handles <see cref="GetLoginAttemptsCountQuery"/>.</summary>
public sealed class GetLoginAttemptsCountQueryHandler(
    ILoginAttemptRepository attempts,
    ILogger<GetLoginAttemptsCountQueryHandler> logger)
    : IRequestHandler<GetLoginAttemptsCountQuery, int>
{
    /// <summary>Returns the total attempt count.</summary>
    public async Task<int> Handle(GetLoginAttemptsCountQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetLoginAttemptsCountQuery));
        return await attempts.CountAsync(cancellationToken);
    }
}
