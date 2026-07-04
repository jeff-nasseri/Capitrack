using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Queries;

/// <summary>Returns the current authenticated session.</summary>
public record GetSessionQuery : IRequest<SessionDto>;

/// <summary>Handles <see cref="GetSessionQuery"/>.</summary>
public sealed class GetSessionQueryHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    ILogger<GetSessionQueryHandler> logger)
    : IRequestHandler<GetSessionQuery, SessionDto>
{
    /// <summary>Resolves the current user and returns the resulting session.</summary>
    public async Task<SessionDto> Handle(GetSessionQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetSessionQuery));

        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");

        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken)
                   ?? throw new UnauthorizedException("Not authenticated");

        return new SessionDto(user.Username, user.BaseCurrency.Value, user.TwoFactorEnabled);
    }
}
