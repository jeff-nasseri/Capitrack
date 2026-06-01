using Server.Application.Common;

namespace Server.Application.Settings.Queries;

/// <summary>Returns application metadata for the settings screen.</summary>
public record GetSettingsInfoQuery : IRequest<AppMetaDto>;

/// <summary>Handles <see cref="GetSettingsInfoQuery"/>.</summary>
public sealed class GetSettingsInfoQueryHandler(
    ISystemService system,
    ILogger<GetSettingsInfoQueryHandler> logger)
    : IRequestHandler<GetSettingsInfoQuery, AppMetaDto>
{
    /// <summary>Assembles the application metadata from the system service and static app info.</summary>
    public Task<AppMetaDto> Handle(GetSettingsInfoQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetSettingsInfoQuery));

        return Task.FromResult(new AppMetaDto(
            system.DbPath,
            AppInfo.Version,
            AppInfo.Name,
            AppInfo.Repository,
            AppInfo.License));
    }
}
