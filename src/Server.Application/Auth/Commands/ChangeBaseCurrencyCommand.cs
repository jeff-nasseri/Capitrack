using Server.Application.Common.Exceptions;

namespace Server.Application.Auth.Commands;

public record ChangeBaseCurrencyCommand(string? BaseCurrency) : IRequest;

public sealed class ChangeBaseCurrencyValidator : AbstractValidator<ChangeBaseCurrencyCommand>
{
    public ChangeBaseCurrencyValidator() =>
        RuleFor(x => x.BaseCurrency).NotEmpty().WithMessage("Base currency required");
}

public sealed class ChangeBaseCurrencyHandler(ICurrentUser currentUser, IUserRepository users, IUnitOfWork uow)
    : IRequestHandler<ChangeBaseCurrencyCommand>
{
    public async Task Handle(ChangeBaseCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException("Not authenticated");

        var user = await users.GetByUsernameAsync(currentUser.Username!, cancellationToken)
                   ?? throw new UnauthorizedException("Not authenticated");

        user.ChangeBaseCurrency(CurrencyCode.Create(request.BaseCurrency));
        await uow.SaveChangesAsync(cancellationToken);
    }
}
