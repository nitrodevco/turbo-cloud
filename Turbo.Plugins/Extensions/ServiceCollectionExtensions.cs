using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboPlugins(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<PluginConfig>(
            builder.Configuration.GetSection(PluginConfig.SECTION_NAME)
        );

        services.AddSingleton<PluginManager>();
        services.AddHostedService<PluginBootstrapper>();

        return services;
    }
}
