using Microsoft.Extensions.Configuration;
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
        var pluginSection = builder.Configuration.GetSection(PluginConfig.SECTION_NAME);

        services.Configure<PluginConfig>(pluginSection);

        services.AddSingleton<PluginManager>();
        services.AddHostedService<PluginBootstrapper>();

        // Read through the bound class, not by key name: a string literal here would keep
        // working while the option was renamed, and it answers false for a missing section
        // instead of the option's own default.
        var pluginConfig = pluginSection.Get<PluginConfig>() ?? new PluginConfig();

        if (builder.Environment.IsDevelopment() && pluginConfig.HotReloadEnabled)
            services.AddHostedService<PluginHotReloadService>();

        return services;
    }

    public static IServiceCollection AddHostPlugin<TModule>(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
        where TModule : class, IHostPluginModule, new()
    {
        var module = new TModule();

        module.ConfigureServices(services, builder);

        services.AddSingleton<IHostPluginModule>(module);

        return services;
    }
}
