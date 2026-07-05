using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

/// <summary>Completes a 2FA-protected login: re-checks the password and validates the TOTP code (the second step).</summary>
/// <param name="Username">The username.</param>
/// <param name="Password">The plaintext password (re-sent so the step is stateless).</param>
/// <param name="Code">The 6-digit TOTP code from the authenticator app.</param>
/// <param name="IpAddress">The client IP, populated by the controller.</param>
/// <param name="UserAgent">The client user agent, populated by the controller.</param>
public record VerifyTwoFactorLoginCommand(string? Username, string? Password, string? Code, string? IpAddress, string? UserAgent)
    : IRequest<LoginResultDto>;

/// <summary>Handles <see cref="VerifyTwoFactorLoginCommand"/>.</summary>
public sealed class VerifyTwoFactorLoginHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ITotpService totp,
    ILoginSecurityService security,
    ILogger<VerifyTwoFactorLoginHandler> logger)
    : IRequestHandler<VerifyTwoFactorLoginCommand, LoginResultDto>
{
    /// <summary>Re-verifies the password and TOTP code (a bad code counts toward the auto-block), then returns the session.</summary>
    public async Task<LoginResultDto> Handle(VerifyTwoFactorLoginCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(VerifyTwoFactorLoginCommand));
        var ip = request.IpAddress ?? "unknown";

        if (await security.IsIpBlockedAsync(ip, cancellationToken))
            throw new TooManyRequestsException("Too many failed attempts. This IP is temporarily blocked — try again in a few minutes.");

        var user = await users.GetByUsernameAsync(request.Username ?? "", cancellationToken);
        // Constant-time password check (dummy hash when unknown) to avoid a user-enumeration timing oracle.
        var passwordOk = hasher.Verify(request.Password ?? "", user?.PasswordHash ?? hasher.DummyHash);
        if (user is null || !passwordOk)
        {
            await security.RecordAttemptAsync(request.Username, ip, false, "invalid_password", request.UserAgent, cancellationToken);
            throw new UnauthorizedException("Invalid credentials");
        }

        // This endpoint is only for 2FA-protected accounts. If 2FA isn't enabled, reject rather than
        // signing in on the password alone (the normal /login path handles non-2FA accounts).
        if (!user.TwoFactorEnabled)
            throw new UnauthorizedException("Invalid credentials");

        // The TOTP code is verified unconditionally — a wrong code is a genuine failed attempt, so
        // brute-forcing the code triggers the IP block too.
        if (!totp.VerifyCode(user.TwoFactorSecret, request.Code))
        {
            await security.RecordAttemptAsync(user.Username, ip, false, "invalid_2fa", request.UserAgent, cancellationToken);
            throw new UnauthorizedException("Invalid authentication code");
        }

        await security.RecordAttemptAsync(user.Username, ip, true, "success", request.UserAgent, cancellationToken);
        return LoginResultDto.SignedIn(new SessionDto(user.Username, user.BaseCurrency.Value, user.TwoFactorEnabled, user.SessionLifetimeMinutes));
    }
}
