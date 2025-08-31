using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Plugins;

namespace Turbo.Main.Extensions;

public static class TurboPluginHostBuilderExtensions
{
    public static IHostBuilder UseTurboPlugins(
        this IHostBuilder host,
        string pluginDir,
        IEnumerable<string>? shareAllow = null,
        IEnumerable<string>? shareDeny = null,
        ILogger? logger = null
    )
    {
        var plugins = PluginLoader.LoadPlugins(
            pluginDir,
            logger,
            new PluginLoadPolicy(shareAllow, shareDeny)
        );

        foreach (var p in plugins)
        {
            logger?.LogInformation(
                "Configuring plugin: {PluginName} by {PluginAuthor}",
                p.PluginName,
                p.PluginAuthor
            );

            p.ConfigureHost(host);
        }

        host.ConfigureServices(
            (ctx, services) =>
            {
                foreach (var p in plugins)
                {
                    services.AddSingleton(p.GetType(), p);
                }

                var pluginAssemblies = plugins
                    .Select(p => p.GetType().Assembly)
                    .Distinct()
                    .ToArray();

                services.AddSingleton<IEnumerable<Assembly>>(pluginAssemblies);

                foreach (var p in plugins)
                {
                    p.ConfigureServices(ctx, services);
                }
            }
        );

        return host;
    }
}
