using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

/// <summary>Authenticates a user with a username and password.</summary>
/// <param name="Username">The username.</param>
/// <param name="Password">The plaintext password.</param>
public record LoginCommand(string? Username, string? Password) : IRequest<SessionDto>;

/// <summary>Handles <see cref="LoginCommand"/>.</summary>
public sealed class LoginHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ILogger<LoginHandler> logger)
    : IRequestHandler<LoginCommand, SessionDto>
{
    /// <summary>Verifies the credentials and returns the resulting session.</summary>
    public async Task<SessionDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(LoginCommand));

        var user = await users.GetByUsernameAsync(request.Username ?? "", cancellationToken);
        if (user is null || !hasher.Verify(request.Password ?? "", user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials");

        return new SessionDto(user.Username, user.BaseCurrency.Value);
    }
}
