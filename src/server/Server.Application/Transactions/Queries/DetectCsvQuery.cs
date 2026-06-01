namespace Server.Application.Transactions.Queries;

/// <summary>Detects the CSV format of the supplied content.</summary>
/// <param name="Content">The raw CSV content.</param>
public record DetectCsvQuery(string Content) : IRequest<DetectResultDto>;

/// <summary>Handles <see cref="DetectCsvQuery"/>.</summary>
public sealed class DetectCsvQueryHandler(
    IImporterService importer,
    ILogger<DetectCsvQueryHandler> logger)
    : IRequestHandler<DetectCsvQuery, DetectResultDto>
{
    /// <summary>Delegates CSV format detection to the importer service.</summary>
    public Task<DetectResultDto> Handle(DetectCsvQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(DetectCsvQuery));
        return Task.FromResult(importer.Detect(request.Content));
    }
}
