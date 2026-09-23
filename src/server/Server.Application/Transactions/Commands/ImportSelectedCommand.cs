using Server.Application.Common.Exceptions;

namespace Server.Application.Transactions.Commands;

/// <summary>
/// Imports the rows the user selected in a Check &amp; Import preview. The files themselves are sent
/// again and re-parsed on the server, so what is imported is exactly what was previewed.
/// </summary>
/// <param name="AccountId">The target account's identifier.</param>
/// <param name="Files">The files with their row selections and stake flags.</param>
public record ImportSelectedCommand(int AccountId, List<ImportFileInput> Files) : IRequest<ImportResultDto>;

/// <summary>Handles <see cref="ImportSelectedCommand"/>.</summary>
public sealed class ImportSelectedHandler(
    IAccountRepository accounts,
    IImporterService importer,
    ILogger<ImportSelectedHandler> logger)
    : IRequestHandler<ImportSelectedCommand, ImportResultDto>
{
    /// <summary>Verifies the account exists, then imports the selected rows atomically.</summary>
    public async Task<ImportResultDto> Handle(ImportSelectedCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ImportSelectedCommand));

        if (!await accounts.ExistsAsync(request.AccountId, cancellationToken))
            throw new NotFoundException("Account not found");

        return await importer.ImportFilesAsync(request.AccountId, request.Files);
    }
}
