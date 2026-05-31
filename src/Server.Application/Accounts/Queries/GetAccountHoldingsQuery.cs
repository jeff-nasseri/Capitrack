using Server.Application.Common.Exceptions;
using Server.Domain.Holdings;

namespace Server.Application.Accounts.Queries;

public record GetAccountHoldingsQuery(int Id) : IRequest<List<HoldingDto>>;

public sealed class GetAccountHoldingsQueryHandler(IAccountRepository accounts, ITransactionRepository transactions)
    : IRequestHandler<GetAccountHoldingsQuery, List<HoldingDto>>
{
    public async Task<List<HoldingDto>> Handle(GetAccountHoldingsQuery request, CancellationToken cancellationToken)
    {
        if (!await accounts.ExistsAsync(request.Id, cancellationToken))
            throw new NotFoundException("Account not found");

        var txs = await transactions.ForAccountAsync(request.Id, cancellationToken);
        var holdings = HoldingsCalculator.ForAccount(txs);
        return holdings.Select(h => new HoldingDto(
            h.Symbol.Value, h.Quantity, h.AvgCost, h.TotalCost,
            h.TransactionCount, h.FirstTransaction, h.LastTransaction)).ToList();
    }
}
