using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

public record LoginCommand(string? Username, string? Password) : IRequest<SessionDto>;

public sealed class LoginHandler(IUserRepository users, IPasswordHasher hasher)
    : IRequestHandler<LoginCommand, SessionDto>
{
    public async Task<SessionDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByUsernameAsync(request.Username ?? "", cancellationToken);
        if (user is null || !hasher.Verify(request.Password ?? "", user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials");

        return new SessionDto(user.Username, user.BaseCurrency.Value);
    }
}
