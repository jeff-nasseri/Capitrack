using Server.Application.Common;

namespace Server.Application.Settings.Queries;

/// <summary>Returns about information for the application.</summary>
public record GetAboutQuery : IRequest<AboutDto>;

/// <summary>Handles <see cref="GetAboutQuery"/>.</summary>
public sealed class GetAboutQueryHandler(ILogger<GetAboutQueryHandler> logger)
    : IRequestHandler<GetAboutQuery, AboutDto>
{
    /// <summary>Returns the static about metadata.</summary>
    public Task<AboutDto> Handle(GetAboutQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetAboutQuery));

        return Task.FromResult(new AboutDto(
            AppInfo.Name,
            AppInfo.Description,
            AppInfo.Version,
            AppInfo.License,
            AppInfo.Repository,
            AppInfo.Author,
            OpenSource: true));
    }
}
