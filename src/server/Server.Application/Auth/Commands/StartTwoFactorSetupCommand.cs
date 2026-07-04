using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

/// <summary>Begins 2FA setup for the current user: generates a secret and returns it plus a QR to scan.</summary>
public record StartTwoFactorSetupCommand : IRequest<TwoFactorSetupDto>;

/// <summary>Handles <see cref="StartTwoFactorSetupCommand"/>.</summary>
public sealed class StartTwoFactorSetupHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    ITotpService totp,
    IUnitOfWork uow,
    ILogger<StartTwoFactorSetupHandler> logger)
    : IRequestHandler<StartTwoFactorSetupCommand, TwoFactorSetupDto>
{
    private const string Issuer = "Capitrack";

    /// <summary>Generates and stores a pending TOTP secret (2FA stays disabled until confirmed) and returns the setup payload.</summary>
    public async Task<TwoFactorSetupDto> Handle(StartTwoFactorSetupCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(StartTwoFactorSetupCommand));

        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");
        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken)
                   ?? throw new UnauthorizedException("Not authenticated");

        // Reuse an existing secret rather than minting a fresh one on every call: this makes setup
        // idempotent, so a repeated request can't invalidate a QR the user has already scanned, and
        // it never silently disables an already-active 2FA.
        var secret = string.IsNullOrWhiteSpace(user.TwoFactorSecret) ? totp.GenerateSecret() : user.TwoFactorSecret!;
        if (!user.TwoFactorEnabled)
        {
            user.BeginTwoFactorSetup(secret);
            await uow.SaveChangesAsync(cancellationToken);
        }

        var uri = totp.BuildOtpAuthUri(secret, user.Username, Issuer);
        var svg = totp.GenerateQrSvg(uri);
        return new TwoFactorSetupDto(secret, uri, svg);
    }
}
