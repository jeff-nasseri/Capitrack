namespace Server.Application.Transactions.Queries;

/// <summary>Returns the total number of transactions matching the optional filters, ignoring paging.</summary>
/// <param name="AccountId">An optional account filter.</param>
/// <param name="Symbol">An optional exact-symbol filter.</param>
/// <param name="Search">An optional case-insensitive CONTAINS filter on the symbol.</param>
/// <param name="Type">An optional exact transaction-type filter (e.g. "buy").</param>
public record GetTransactionsCountQuery(int? AccountId, string? Symbol, string? Search, string? Type)
    : IRequest<int>;

/// <summary>Handles <see cref="GetTransactionsCountQuery"/>.</summary>
public sealed class GetTransactionsCountQueryHandler(
    ITransactionRepository transactions,
    ILogger<GetTransactionsCountQueryHandler> logger)
    : IRequestHandler<GetTransactionsCountQuery, int>
{
    /// <summary>Counts every transaction matching the filters, ignoring paging.</summary>
    public async Task<int> Handle(GetTransactionsCountQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetTransactionsCountQuery));

        return await transactions.CountAsync(request.AccountId, request.Symbol, request.Search, request.Type, cancellationToken);
    }
}
