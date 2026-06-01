namespace Server.Application.Settings.Commands;

/// <summary>Signals an application refresh and returns the current database path.</summary>
public record RefreshCommand : IRequest<RefreshResultDto>;

/// <summary>Handles <see cref="RefreshCommand"/>.</summary>
public sealed class RefreshHandler(
    ISystemService system,
    ILogger<RefreshHandler> logger)
    : IRequestHandler<RefreshCommand, RefreshResultDto>
{
    /// <summary>Returns a success message together with the current database path.</summary>
    public Task<RefreshResultDto> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(RefreshCommand));
        return Task.FromResult(new RefreshResultDto("Application refreshed successfully", system.DbPath));
    }
}
