using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Core.Plugins;

namespace Turbo.Plugins.Extensions;

public static class HostBuilderExtensions
{
    public static IHostBuilder UsePluginAssemblies(
        this IHostBuilder host,
        IEnumerable<Assembly> asms,
        ILogger? logger = null
    )
    {
        if (!asms.Any())
            return host;

        var plugins = new List<ITurboPlugin>();

        foreach (var asm in asms)
        {
            var pluginType = asm.GetTypes()
                .Where(t =>
                    typeof(ITurboPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface
                )
                .OrderByDescending(t => t.GetCustomAttribute<TurboPluginAttribute>() != null)
                .FirstOrDefault();

            if (pluginType is null)
                continue;

            if (Activator.CreateInstance(pluginType) is ITurboPlugin plugin)
            {
                plugins.Add(plugin);

                logger?.LogInformation(
                    "Created plugin instance: {PluginName} by {PluginAuthor}",
                    plugin.PluginName,
                    plugin.PluginAuthor
                );
            }
        }

        foreach (var p in plugins)
            p.ConfigureHost(host);

        host.ConfigureServices(
            (ctx, services) =>
            {
                services.AddSingleton(asms);

                foreach (var p in plugins)
                {
                    services.AddSingleton(p.GetType(), p);

                    p.ConfigureServices(ctx, services);
                }
            }
        );

        return host;
    }
}
