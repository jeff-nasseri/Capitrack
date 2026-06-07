namespace Server.Application.Transactions.Commands;

/// <summary>Parses a single import file for preview (no insert), flagging duplicates and stake-eligible rows.</summary>
/// <param name="FileName">The uploaded file's name.</param>
/// <param name="Content">The raw CSV content.</param>
/// <param name="AccountId">The account the rows would be imported into (for duplicate detection).</param>
public record PreviewImportCommand(string FileName, string Content, int AccountId)
    : IRequest<PreviewFileDto>;

/// <summary>Handles <see cref="PreviewImportCommand"/>.</summary>
public sealed class PreviewImportHandler(
    IImporterService importer,
    ILogger<PreviewImportHandler> logger)
    : IRequestHandler<PreviewImportCommand, PreviewFileDto>
{
    /// <summary>Delegates to the importer's parse-only preview.</summary>
    public async Task<PreviewFileDto> Handle(PreviewImportCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(PreviewImportCommand));
        return await importer.PreviewAsync(request.FileName, request.Content, request.AccountId);
    }
}
