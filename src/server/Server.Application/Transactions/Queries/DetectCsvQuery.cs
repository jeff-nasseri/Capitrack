namespace Server.Application.Transactions.Queries;

public record DetectCsvQuery(string Content) : IRequest<DetectResultDto>;

public sealed class DetectCsvQueryHandler(IImporterService importer)
    : IRequestHandler<DetectCsvQuery, DetectResultDto>
{
    public Task<DetectResultDto> Handle(DetectCsvQuery request, CancellationToken cancellationToken)
        => Task.FromResult(importer.Detect(request.Content));
}
