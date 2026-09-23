namespace Server.Application.Transactions.Commands;

/// <summary>Previews importing files into an account (no insert): row statuses, legs and reconciliation.</summary>
/// <param name="AccountId">The account the rows would be imported into.</param>
/// <param name="Files">The uploaded files with optional row selections.</param>
public record PreviewImportCommand(int AccountId, List<ImportFileInput> Files) : IRequest<ImportPreviewDto>;

/// <summary>Handles <see cref="PreviewImportCommand"/>.</summary>
public sealed class PreviewImportHandler(
    IImporterService importer,
    ILogger<PreviewImportHandler> logger)
    : IRequestHandler<PreviewImportCommand, ImportPreviewDto>
{
    /// <summary>Delegates to the importer's plan-only preview.</summary>
    public async Task<ImportPreviewDto> Handle(PreviewImportCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(PreviewImportCommand));
        return await importer.PreviewAsync(request.AccountId, request.Files);
    }
}
