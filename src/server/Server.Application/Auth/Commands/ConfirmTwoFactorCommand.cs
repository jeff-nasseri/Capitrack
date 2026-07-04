using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

/// <summary>Confirms and activates 2FA by validating a code against the pending secret.</summary>
/// <param name="Code">The 6-digit code from the authenticator app.</param>
/// <param name="IpAddress">The client IP, populated by the controller (never trusted from the body).</param>
/// <param name="UserAgent">The client user agent, populated by the controller.</param>
public record ConfirmTwoFactorCommand(string? Code, string? IpAddress, string? UserAgent) : IRequest;

/// <summary>Handles <see cref="ConfirmTwoFactorCommand"/>.</summary>
public sealed class ConfirmTwoFactorHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    ITotpService totp,
    ILoginSecurityService security,
    IUnitOfWork uow,
    ILogger<ConfirmTwoFactorHandler> logger)
    : IRequestHandler<ConfirmTwoFactorCommand>
{
    /// <summary>Verifies the code against the pending secret and activates 2FA (rate-limited by the IP block).</summary>
    public async Task Handle(ConfirmTwoFactorCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ConfirmTwoFactorCommand));

        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");
        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken)
                   ?? throw new UnauthorizedException("Not authenticated");

        // Throttle code-guessing: a blocked IP is refused, and each wrong code counts toward the block.
        var ip = request.IpAddress ?? "unknown";
        if (await security.IsIpBlockedAsync(ip, cancellationToken))
            throw new TooManyRequestsException("Too many failed attempts. Try again in a few minutes.");

        // VerifyCode returns false when no setup secret exists, so this also guards "setup not started".
        if (!totp.VerifyCode(user.TwoFactorSecret, request.Code))
        {
            await security.RecordAttemptAsync(user.Username, ip, false, "invalid_2fa_confirm", request.UserAgent, cancellationToken);
            throw new UnauthorizedException("Invalid authentication code");
        }

        user.ConfirmTwoFactor();
        await uow.SaveChangesAsync(cancellationToken);
    }
}
