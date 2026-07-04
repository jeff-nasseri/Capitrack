using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

/// <summary>Authenticates a user with a username and password (the first login step).</summary>
/// <param name="Username">The username.</param>
/// <param name="Password">The plaintext password.</param>
/// <param name="IpAddress">The client IP, populated by the controller from the request.</param>
/// <param name="UserAgent">The client user agent, populated by the controller.</param>
public record LoginCommand(string? Username, string? Password, string? IpAddress, string? UserAgent)
    : IRequest<LoginResultDto>;

/// <summary>Handles <see cref="LoginCommand"/>.</summary>
public sealed class LoginHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ILoginSecurityService security,
    ILogger<LoginHandler> logger)
    : IRequestHandler<LoginCommand, LoginResultDto>
{
    /// <summary>Rejects blocked IPs, verifies the password, and either signs in or requests the second factor.</summary>
    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(LoginCommand));
        var ip = request.IpAddress ?? "unknown";

        // Blocked IPs are rejected before any credential check — no information leak, no timing oracle.
        // We do NOT record blocked hits: the block is already audited, and skipping them bounds how
        // much a hammering attacker can grow the attempts table while blocked.
        if (await security.IsIpBlockedAsync(ip, cancellationToken))
            throw new TooManyRequestsException("Too many failed attempts. This IP is temporarily blocked — try again in a few minutes.");

        var user = await users.GetByUsernameAsync(request.Username ?? "", cancellationToken);
        // Always run the hash verification (against a dummy hash when the user is unknown) so the
        // response time doesn't reveal whether the username exists.
        var passwordOk = hasher.Verify(request.Password ?? "", user?.PasswordHash ?? hasher.DummyHash);
        if (user is null || !passwordOk)
        {
            await security.RecordAttemptAsync(request.Username, ip, false, "invalid_password", request.UserAgent, cancellationToken);
            throw new UnauthorizedException("Invalid credentials");
        }

        // Password correct — if 2FA is on, do NOT sign in yet; the controller must not set the cookie.
        if (user.TwoFactorEnabled)
            return LoginResultDto.Requires2Fa();

        await security.RecordAttemptAsync(user.Username, ip, true, "success", request.UserAgent, cancellationToken);
        return LoginResultDto.SignedIn(new SessionDto(user.Username, user.BaseCurrency.Value, user.TwoFactorEnabled));
    }
}
