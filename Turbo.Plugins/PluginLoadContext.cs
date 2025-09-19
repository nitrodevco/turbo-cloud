using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Turbo.Plugins;

public sealed class PluginLoadContext(string pluginDir) : AssemblyLoadContext(isCollectible: true)
{
    private readonly string _dir = pluginDir;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = Path.Combine(_dir, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}
