using Server.Application.Settings;

namespace Server.Application.Settings.Queries;

public record GetDatabaseQuery : IRequest<DatabaseInfoDto>;

public sealed class GetDatabaseQueryHandler(ISystemService system)
    : IRequestHandler<GetDatabaseQuery, DatabaseInfoDto>
{
    public Task<DatabaseInfoDto> Handle(GetDatabaseQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new DatabaseInfoDto(system.DbPath, system.DatabaseExists()));
}
