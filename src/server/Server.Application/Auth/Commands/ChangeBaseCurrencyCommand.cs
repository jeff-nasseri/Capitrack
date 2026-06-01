using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

/// <summary>Changes the current user's base currency.</summary>
/// <param name="BaseCurrency">The new base currency code (required).</param>
public record ChangeBaseCurrencyCommand(string? BaseCurrency) : IRequest;

/// <summary>Validates <see cref="ChangeBaseCurrencyCommand"/>.</summary>
public sealed class ChangeBaseCurrencyValidator : AbstractValidator<ChangeBaseCurrencyCommand>
{
    /// <summary>Configures the validation rules.</summary>
    public ChangeBaseCurrencyValidator() =>
        RuleFor(x => x.BaseCurrency).NotEmpty().WithMessage("Base currency required");
}

/// <summary>Handles <see cref="ChangeBaseCurrencyCommand"/>.</summary>
public sealed class ChangeBaseCurrencyHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IUnitOfWork uow,
    ILogger<ChangeBaseCurrencyHandler> logger)
    : IRequestHandler<ChangeBaseCurrencyCommand>
{
    /// <summary>Updates and persists the current user's base currency.</summary>
    public async Task Handle(ChangeBaseCurrencyCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ChangeBaseCurrencyCommand));

        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");

        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken)
                   ?? throw new UnauthorizedException("Not authenticated");

        user.ChangeBaseCurrency(CurrencyCode.Create(request.BaseCurrency));
        await uow.SaveChangesAsync(cancellationToken);
    }
}
