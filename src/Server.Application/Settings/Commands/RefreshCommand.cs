using Server.Application.Settings;

namespace Server.Application.Settings.Commands;

public record RefreshCommand : IRequest<RefreshResultDto>;

public sealed class RefreshHandler(ISystemService system)
    : IRequestHandler<RefreshCommand, RefreshResultDto>
{
    public Task<RefreshResultDto> Handle(RefreshCommand request, CancellationToken cancellationToken)
        => Task.FromResult(new RefreshResultDto("Application refreshed successfully", system.DbPath));
}
