using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Queries;

public record GetSessionQuery : IRequest<SessionDto>;

public sealed class GetSessionQueryHandler(ICurrentUser currentUser, IUserRepository users)
    : IRequestHandler<GetSessionQuery, SessionDto>
{
    public async Task<SessionDto> Handle(GetSessionQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");

        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken)
                   ?? throw new UnauthorizedException("Not authenticated");

        return new SessionDto(user.Username, user.BaseCurrency.Value);
    }
}
