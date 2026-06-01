using Server.Application.Common.Exceptions;

namespace Server.Application.Transactions.Commands;

/// <summary>Imports transactions from CSV content into an account.</summary>
/// <param name="Content">The raw CSV content.</param>
/// <param name="AccountId">The target account's identifier.</param>
/// <param name="Format">An optional CSV format hint.</param>
public record ImportTransactionsCsvCommand(string Content, int AccountId, string? Format)
    : IRequest<ImportResultDto>;

/// <summary>Handles <see cref="ImportTransactionsCsvCommand"/>.</summary>
public sealed class ImportTransactionsCsvHandler(
    IAccountRepository accounts,
    IImporterService importer,
    ILogger<ImportTransactionsCsvHandler> logger)
    : IRequestHandler<ImportTransactionsCsvCommand, ImportResultDto>
{
    /// <summary>Verifies the account exists and delegates to the importer service.</summary>
    public async Task<ImportResultDto> Handle(ImportTransactionsCsvCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ImportTransactionsCsvCommand));

        if (!await accounts.ExistsAsync(request.AccountId, cancellationToken))
            throw new NotFoundException("Account not found");

        return await importer.ImportAsync(request.Content, request.AccountId, request.Format);
    }
}
