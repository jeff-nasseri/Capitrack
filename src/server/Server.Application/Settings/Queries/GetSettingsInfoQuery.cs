using Server.Application.Common;
using Server.Application.Settings;

namespace Server.Application.Settings.Queries;

public record GetSettingsInfoQuery : IRequest<AppMetaDto>;

public sealed class GetSettingsInfoQueryHandler(ISystemService system)
    : IRequestHandler<GetSettingsInfoQuery, AppMetaDto>
{
    public Task<AppMetaDto> Handle(GetSettingsInfoQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new AppMetaDto(
            system.DbPath,
            AppInfo.Version,
            AppInfo.Name,
            AppInfo.Repository,
            AppInfo.License));
}
