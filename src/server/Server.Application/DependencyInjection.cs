using Microsoft.Extensions.DependencyInjection;
using Server.Application.Common.Behaviors;

namespace Server.Application;

/// <summary>Registers the application layer: MediatR, validators, AutoMapper and pipeline behaviors.</summary>
public static class DependencyInjection
{
    /// <summary>Adds the application-layer services to the DI container.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(typeof(DependencyInjection).Assembly);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
