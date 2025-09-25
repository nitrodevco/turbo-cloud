using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Logging.Factories;

namespace Turbo.Logging.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboLogging(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<TurboConsoleFormatterOptions>(
            builder.Configuration.GetSection(TurboConsoleFormatterOptions.SECTION_NAME)
        );

        var factory = LoggerFactory.Create(build =>
        {
            build.ClearProviders();
            build.AddConfiguration(builder.Configuration.GetSection("Logging"));
            build.AddTurboConsoleLogger();
        });

        services.AddSingleton(factory);
        services.Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));

        return services;
    }

    public static IServiceCollection ConfigurePrefixedLogging(
        this IServiceCollection services,
        IServiceProvider host,
        string prefix
    )
    {
        services.AddSingleton<IPrefixedLoggerFactory>(sp => new PrefixedLoggerFactory(
            host.GetRequiredService<ILoggerFactory>(),
            prefix
        ));

        services.Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(PrefixedLogger<>)));

        return services;
    }
}
