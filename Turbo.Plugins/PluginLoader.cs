using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Turbo.Core.Plugins;

namespace Turbo.Plugins;

public class PluginLoader
{
    public static IReadOnlyList<ITurboPlugin> LoadPlugins(
        string directory,
        ILogger? logger,
        PluginLoadPolicy? policy = null
    )
    {
        var plugins = new List<ITurboPlugin>();

        foreach (var dll in Directory.EnumerateFiles(directory, "*.dll"))
        {
            var ctx = new TurboPluginLoadContext(
                dll,
                policy?.ShareAllowPatterns,
                policy?.ShareDenyPatterns
            );

            var asm = ctx.LoadFromAssemblyPath(dll);
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
                    "Loading Plugin: {PluginName} by {PluginAuthor}",
                    plugin.PluginName,
                    plugin.PluginAuthor
                );
            }
        }

        return plugins;
    }
}
