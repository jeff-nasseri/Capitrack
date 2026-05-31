using Server.Application.Common.Exceptions;

namespace Server.Application.Transactions.Commands;

public record ImportTransactionsCsvCommand(string Content, int AccountId, string? Format)
    : IRequest<ImportResultDto>;

public sealed class ImportTransactionsCsvHandler(IAccountRepository accounts, IImporterService importer)
    : IRequestHandler<ImportTransactionsCsvCommand, ImportResultDto>
{
    public async Task<ImportResultDto> Handle(ImportTransactionsCsvCommand request, CancellationToken cancellationToken)
    {
        if (!await accounts.ExistsAsync(request.AccountId, cancellationToken))
            throw new NotFoundException("Account not found");

        return await importer.ImportAsync(request.Content, request.AccountId, request.Format);
    }
}
