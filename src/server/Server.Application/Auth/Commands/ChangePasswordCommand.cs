using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

/// <summary>Changes the current user's password.</summary>
/// <param name="CurrentPassword">The user's current password.</param>
/// <param name="NewPassword">The new password (must meet the complexity policy).</param>
public record ChangePasswordCommand(
    [property: System.Text.Json.Serialization.JsonPropertyName("currentPassword")] string CurrentPassword,
    [property: System.Text.Json.Serialization.JsonPropertyName("newPassword")] string NewPassword) : IRequest;

/// <summary>Validates <see cref="ChangePasswordCommand"/>.</summary>
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    private const string PasswordMessage =
        "Password must be at least 8 characters with uppercase, lowercase, number, and special character";

    /// <summary>Configures the password complexity rules.</summary>
    public ChangePasswordValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage(PasswordMessage);
        RuleFor(x => x.NewPassword).MinimumLength(8).WithMessage(PasswordMessage);
        RuleFor(x => x.NewPassword).Matches("[A-Z]").WithMessage(PasswordMessage);
        RuleFor(x => x.NewPassword).Matches("[a-z]").WithMessage(PasswordMessage);
        RuleFor(x => x.NewPassword).Matches("[0-9]").WithMessage(PasswordMessage);
        RuleFor(x => x.NewPassword).Matches("[!@#$%^&*]").WithMessage(PasswordMessage);
    }
}

/// <summary>Handles <see cref="ChangePasswordCommand"/>.</summary>
public sealed class ChangePasswordHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IPasswordHasher hasher,
    IUnitOfWork uow,
    ILogger<ChangePasswordHandler> logger)
    : IRequestHandler<ChangePasswordCommand>
{
    /// <summary>Verifies the current password and persists the new hash.</summary>
    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ChangePasswordCommand));

        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");

        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken);
        if (user is null || !hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedException("Current password is incorrect");

        user.ChangePassword(hasher.Hash(request.NewPassword));
        await uow.SaveChangesAsync(cancellationToken);
    }
}
