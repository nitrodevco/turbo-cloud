using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Database.Configuration;
using Turbo.Database.Delegates;

namespace Turbo.Plugins.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection ConfigurePlugin(
        this IServiceCollection services,
        IServiceProvider host,
        ITurboPlugin plugin,
        PluginManifest manifest
    )
    {
        services.AddSingleton(host.GetRequiredService<ILoggerFactory>());
        services.TryAddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(host.GetRequiredService<IOptions<DatabaseConfig>>());
        services.AddSingleton(manifest);
        services.AddSingleton<TablePrefixProvider>(sp =>
        {
            var manifest = sp.GetRequiredService<PluginManifest>();

            return () => manifest.TablePrefix ?? string.Empty;
        });

        if (plugin.RequiredHostServices?.Count > 0)
        {
            foreach (var t in plugin.RequiredHostServices)
            {
                services.AddSingleton(t, _ => host.GetRequiredService(t));
            }
        }

        plugin.ConfigureServices(services);

        return services;
    }
}
