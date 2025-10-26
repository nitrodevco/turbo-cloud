using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
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

    public static IServiceCollection AddHostPlugin<TModule>(this IServiceCollection services)
        where TModule : class, IHostPluginModule, new()
    {
        var module = new TModule();

        module.ConfigureServices(services);

        return services;
    }
}
