using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Database.Configuration;
using Turbo.Database.Delegates;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseTurboPlugins(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        services.AddOptions<PluginConfig>().Bind(cfg.GetSection(PluginConfig.SECTION_NAME));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PluginConfig>>().Value);

        services.AddSingleton<PluginManager>();

        return services;
    }

    internal static IServiceCollection ConfigurePlugin(
        this IServiceCollection services,
        IServiceProvider host,
        ITurboPlugin plugin,
        PluginManifest manifest
    )
    {
        services.AddLogging();
        services.AddSingleton(host.GetRequiredService<DatabaseConfig>());
        services.AddSingleton(host.GetRequiredService<ILoggerFactory>());
        services.AddSingleton(manifest);
        services.AddSingleton<TablePrefixProvider>(sp =>
        {
            var manifest = sp.GetRequiredService<PluginManifest>();

            return () => manifest.TablePrefix ?? string.Empty;
        });

        if (plugin.RequiredHostServices?.Count > 0)
        {
            foreach (var t in plugin.RequiredHostServices)
                services.AddSingleton(_ => host.GetRequiredService(t));
        }

        services.ConfigurePluginServices(plugin);

        return services;
    }

    internal static IServiceCollection ConfigurePluginServices(
        this IServiceCollection services,
        ITurboPlugin plugin
    )
    {
        plugin.ConfigureServices(services);

        return services;
    }
}
