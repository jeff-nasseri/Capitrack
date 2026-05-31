using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Server.Application.Common.Behaviors;

/// <summary>MediatR pipeline behavior that logs each request and how long its handler took.</summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        _logger.LogDebug("Handling {Request}", name);
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();
        _logger.LogDebug("Handled {Request} in {Elapsed}ms", name, sw.ElapsedMilliseconds);
        return response;
    }
}
