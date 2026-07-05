using Server.Application.Common.Exceptions;
using Server.Domain.Users;

namespace Server.Application.Auth.Commands;

/// <summary>Changes how long a signed-in session stays valid without activity.</summary>
/// <param name="Minutes">The new lifetime in minutes (15–120).</param>
public record ChangeSessionLifetimeCommand(int? Minutes) : IRequest;

/// <summary>Validates <see cref="ChangeSessionLifetimeCommand"/>.</summary>
public sealed class ChangeSessionLifetimeValidator : AbstractValidator<ChangeSessionLifetimeCommand>
{
    /// <summary>Requires a lifetime within the allowed 15-minute to 2-hour range.</summary>
    public ChangeSessionLifetimeValidator() =>
        RuleFor(x => x.Minutes)
            .NotNull().WithMessage("A session lifetime is required.")
            .InclusiveBetween(User.MinSessionLifetimeMinutes, User.MaxSessionLifetimeMinutes)
            .WithMessage($"Session lifetime must be between {User.MinSessionLifetimeMinutes} minutes and {User.MaxSessionLifetimeMinutes / 60} hours.");
}

/// <summary>Handles <see cref="ChangeSessionLifetimeCommand"/>.</summary>
public sealed class ChangeSessionLifetimeHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IUnitOfWork uow,
    ILogger<ChangeSessionLifetimeHandler> logger)
    : IRequestHandler<ChangeSessionLifetimeCommand>
{
    /// <summary>Persists the new lifetime; the cookie middleware picks it up on the next request.</summary>
    public async Task Handle(ChangeSessionLifetimeCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ChangeSessionLifetimeCommand));

        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");
        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken)
                   ?? throw new UnauthorizedException("Not authenticated");

        user.ChangeSessionLifetime(request.Minutes!.Value);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
