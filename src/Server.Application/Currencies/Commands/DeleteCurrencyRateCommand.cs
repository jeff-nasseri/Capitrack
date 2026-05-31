using Server.Application.Common.Exceptions;

namespace Server.Application.Currencies.Commands;

public record DeleteCurrencyRateCommand(int Id) : IRequest;

public sealed class DeleteCurrencyRateHandler(ICurrencyRateRepository rates, IUnitOfWork uow)
    : IRequestHandler<DeleteCurrencyRateCommand>
{
    public async Task Handle(DeleteCurrencyRateCommand request, CancellationToken cancellationToken)
    {
        var rate = await rates.GetAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Rate not found");
        rates.Remove(rate);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
