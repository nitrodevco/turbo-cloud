using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Turbo.Plugins;

internal sealed class PluginLoadContext(string mainAssemblyPath)
    : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            return LoadFromAssemblyPath(path);

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (!string.IsNullOrEmpty(libPath) && File.Exists(libPath))
            return LoadUnmanagedDllFromPath(libPath);

        return base.LoadUnmanagedDll(unmanagedDllName);
    }
}
