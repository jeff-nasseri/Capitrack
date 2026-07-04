using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

/// <summary>Disables 2FA after re-verifying the account password.</summary>
/// <param name="Password">The account password, required to turn 2FA off.</param>
/// <param name="IpAddress">The client IP, populated by the controller (never trusted from the body).</param>
/// <param name="UserAgent">The client user agent, populated by the controller.</param>
public record DisableTwoFactorCommand(string? Password, string? IpAddress, string? UserAgent) : IRequest;

/// <summary>Handles <see cref="DisableTwoFactorCommand"/>.</summary>
public sealed class DisableTwoFactorHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IPasswordHasher hasher,
    ILoginSecurityService security,
    IUnitOfWork uow,
    ILogger<DisableTwoFactorHandler> logger)
    : IRequestHandler<DisableTwoFactorCommand>
{
    /// <summary>Verifies the password (rate-limited by the IP block), then turns 2FA off and discards the secret.</summary>
    public async Task Handle(DisableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(DisableTwoFactorCommand));

        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");
        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken)
                   ?? throw new UnauthorizedException("Not authenticated");

        // A stolen session cookie must not allow unlimited password guesses to strip 2FA off: a
        // blocked IP is refused, and each wrong password counts toward the block.
        var ip = request.IpAddress ?? "unknown";
        if (await security.IsIpBlockedAsync(ip, cancellationToken))
            throw new TooManyRequestsException("Too many failed attempts. Try again in a few minutes.");

        if (!hasher.Verify(request.Password ?? "", user.PasswordHash))
        {
            await security.RecordAttemptAsync(user.Username, ip, false, "invalid_password_disable", request.UserAgent, cancellationToken);
            throw new UnauthorizedException("Password is incorrect");
        }

        user.DisableTwoFactor();
        await uow.SaveChangesAsync(cancellationToken);
    }
}
