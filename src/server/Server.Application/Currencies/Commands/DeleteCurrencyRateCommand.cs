using Server.Application.Common.Exceptions;

namespace Server.Application.Currencies.Commands;

/// <summary>Deletes a currency-conversion rate by id.</summary>
/// <param name="Id">The rate's identifier.</param>
public record DeleteCurrencyRateCommand(int Id) : IRequest;

/// <summary>Handles <see cref="DeleteCurrencyRateCommand"/>.</summary>
public sealed class DeleteCurrencyRateHandler(
    ICurrencyRateRepository rates,
    IUnitOfWork uow,
    ILogger<DeleteCurrencyRateHandler> logger)
    : IRequestHandler<DeleteCurrencyRateCommand>
{
    /// <summary>Loads and deletes the rate, then persists the change.</summary>
    public async Task Handle(DeleteCurrencyRateCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(DeleteCurrencyRateCommand));

        var rate = await rates.GetAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Rate not found");
        rates.Remove(rate);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
