using Server.Application.Common.Exceptions;

namespace Server.Application.Transactions.Commands;

/// <summary>Imports a user-selected set of transactions (from a Check &amp; Import preview) into an account.</summary>
/// <param name="AccountId">The target account's identifier.</param>
/// <param name="Transactions">The transactions the user chose to import (with any per-row stake flags).</param>
public record ImportSelectedCommand(int AccountId, List<SelectedTransactionDto> Transactions)
    : IRequest<ImportResultDto>;

/// <summary>Handles <see cref="ImportSelectedCommand"/>.</summary>
public sealed class ImportSelectedHandler(
    IAccountRepository accounts,
    IImporterService importer,
    ILogger<ImportSelectedHandler> logger)
    : IRequestHandler<ImportSelectedCommand, ImportResultDto>
{
    /// <summary>Verifies the account exists, then imports the selected transactions (with fingerprint dedup).</summary>
    public async Task<ImportResultDto> Handle(ImportSelectedCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ImportSelectedCommand));

        if (!await accounts.ExistsAsync(request.AccountId, cancellationToken))
            throw new NotFoundException("Account not found");

        return await importer.ImportSelectedAsync(request.AccountId, request.Transactions ?? []);
    }
}
