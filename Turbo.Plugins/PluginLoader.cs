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
    public static IEnumerable<Assembly> GetPluginAssemblies(string directory)
    {
        var assemblies = new List<Assembly>();

        foreach (var dll in Directory.EnumerateFiles(directory, "*.dll"))
        {
            try
            {
                var ctx = new TurboPluginLoadContext(
                    dll,
                    [@"^Turbo\.", @"^Microsoft\.Extensions\.", @"^System(\..+)?$"],
                    []
                );

                var asm = ctx.LoadFromAssemblyPath(dll);

                assemblies.Add(asm);
            }
            catch
            {
                // ignore load failures
            }
        }

        return assemblies.Distinct();
    }
}
