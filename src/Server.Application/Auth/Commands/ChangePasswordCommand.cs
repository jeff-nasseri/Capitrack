using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

public record ChangePasswordCommand(
    [property: System.Text.Json.Serialization.JsonPropertyName("currentPassword")] string CurrentPassword,
    [property: System.Text.Json.Serialization.JsonPropertyName("newPassword")] string NewPassword) : IRequest;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    private const string PasswordMessage =
        "Password must be at least 8 characters with uppercase, lowercase, number, and special character";

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

public sealed class ChangePasswordHandler(ICurrentUser currentUser, IUserRepository users, IPasswordHasher hasher, IUnitOfWork uow)
    : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");

        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken);
        if (user is null || !hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedException("Current password is incorrect");

        user.ChangePassword(hasher.Hash(request.NewPassword));
        await uow.SaveChangesAsync(cancellationToken);
    }
}
