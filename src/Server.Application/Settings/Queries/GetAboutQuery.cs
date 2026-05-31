using Server.Application.Common;
using Server.Application.Settings;

namespace Server.Application.Settings.Queries;

public record GetAboutQuery : IRequest<AboutDto>;

public sealed class GetAboutQueryHandler : IRequestHandler<GetAboutQuery, AboutDto>
{
    public Task<AboutDto> Handle(GetAboutQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new AboutDto(
            AppInfo.Name,
            AppInfo.Description,
            AppInfo.Version,
            AppInfo.License,
            AppInfo.Repository,
            AppInfo.Author,
            OpenSource: true));
}
