using Server.Application.Common.Exceptions;
using Server.Domain.Holdings;

namespace Server.Application.Accounts.Queries;

/// <summary>Returns the computed holdings for an account.</summary>
/// <param name="Id">The account's identifier.</param>
public record GetAccountHoldingsQuery(int Id) : IRequest<List<HoldingDto>>;

/// <summary>Handles <see cref="GetAccountHoldingsQuery"/>.</summary>
public sealed class GetAccountHoldingsQueryHandler(
    IAccountRepository accounts,
    ITransactionRepository transactions,
    IMapper mapper,
    ILogger<GetAccountHoldingsQueryHandler> logger)
    : IRequestHandler<GetAccountHoldingsQuery, List<HoldingDto>>
{
    /// <summary>Verifies the account exists, computes its holdings and returns the resulting DTOs.</summary>
    public async Task<List<HoldingDto>> Handle(GetAccountHoldingsQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetAccountHoldingsQuery));

        if (!await accounts.ExistsAsync(request.Id, cancellationToken))
            throw new NotFoundException("Account not found");

        var txs = await transactions.ForAccountAsync(request.Id, cancellationToken);
        var holdings = HoldingsCalculator.ForAccount(txs);
        return holdings.Select(h => mapper.Map<HoldingDto>(h)).ToList();
    }
}
